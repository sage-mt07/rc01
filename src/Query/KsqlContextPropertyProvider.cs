using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Query.Schema;
using System;
using System.Linq;
using System.Linq.Expressions;

namespace Kafka.Ksql.Linq.Query.Abstractions;

/// <summary>
/// クエリビルダーの最小限インターフェース
/// </summary>
public interface IQueryBuilder<T> where T : class
{
    /// <summary>
    /// ソース型を指定してクエリを定義
    /// </summary>
    IQueryBuilder<T> FromSource<TSource>(Expression<Func<IQueryable<TSource>, IQueryable<T>>> queryExpression) 
        where TSource : class;

    /// <summary>
    /// 自動キー抽出を有効化
    /// </summary>
    IQueryBuilder<T> WithKeyExtraction(bool auto = true);

    /// <summary>
    /// Stream/Table指定
    /// </summary>
    IQueryBuilder<T> AsTable(string? topicName = null);
    IQueryBuilder<T> AsStream(string? topicName = null);

    /// <summary>
    /// QuerySchemaを取得
    /// </summary>
    QuerySchema GetSchema();
}

/// <summary>
/// QueryBuilderの実装
/// </summary>
internal class QueryBuilder<T> : IQueryBuilder<T> where T : class
{
    private Expression? _queryExpression;
    private Type? _sourceType;
    private bool _autoKeyExtraction = true;
    private string? _topicName;

    public IQueryBuilder<T> FromSource<TSource>(Expression<Func<IQueryable<TSource>, IQueryable<T>>> queryExpression) 
        where TSource : class
    {
        _queryExpression = queryExpression;
        _sourceType = typeof(TSource);
        return this;
    }

    public IQueryBuilder<T> WithKeyExtraction(bool auto = true)
    {
        _autoKeyExtraction = auto;
        return this;
    }

    public IQueryBuilder<T> AsTable(string? topicName = null)
    {
        _topicName = topicName;
        return this;
    }

    public IQueryBuilder<T> AsStream(string? topicName = null)
    {
        _topicName = topicName;
        return this;
    }

    public QuerySchema GetSchema()
    {
        if (_queryExpression == null || _sourceType == null)
        {
            return new QuerySchema
            {
                TargetType = typeof(T),
                IsValid = false,
                Errors = { "Query expression or source type not specified" }
            };
        }

        // リフレクションでAnalyzeQueryを呼び出し
        var analyzeMethod = typeof(Analysis.QueryAnalyzer)
            .GetMethod(nameof(Analysis.QueryAnalyzer.AnalyzeQuery))
            ?.MakeGenericMethod(_sourceType, typeof(T));

        if (analyzeMethod?.Invoke(null, new object[] { _queryExpression, _autoKeyExtraction }) is QuerySchemaResult result)
        {
            if (result.Success && result.Schema != null)
            {
                // 追加設定を適用
                if (!string.IsNullOrEmpty(_topicName))
                    result.Schema.TopicName = _topicName;

                return result.Schema;
            }
        }

        return new QuerySchema
        {
            TargetType = typeof(T),
            IsValid = false,
            Errors = { "Failed to analyze query expression" }
        };
    }
}