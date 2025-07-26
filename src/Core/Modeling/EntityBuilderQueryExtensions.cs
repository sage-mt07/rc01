using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Query.Abstractions;
using Kafka.Ksql.Linq.Query.Schema;
using System;
using System.Linq;
using System.Linq.Expressions;

namespace Kafka.Ksql.Linq.Core.Modeling;

/// <summary>
/// EntityBuilderにクエリ定義機能を追加する拡張メソッド
/// </summary>
public static class EntityBuilderQueryExtensions
{
    /// <summary>
    /// LINQ式からKey/Value構造を自動抽出してエンティティを設定
    /// </summary>
    public static EntityModelBuilder<T> HasQuery<T, TSource>(
        this EntityModelBuilder<T> builder,
        Expression<Func<IQueryable<TSource>, IQueryable<T>>> queryExpression)
        where T : class
        where TSource : class
    {
        // クエリ解析
        var result = Kafka.Ksql.Linq.Query.Analysis.QueryAnalyzer.AnalyzeQuery(queryExpression);
        
        if (!result.Success || result.Schema == null)
        {
            throw new InvalidOperationException($"Query analysis failed: {result.ErrorMessage}");
        }

        var schema = result.Schema;
        var entityModel = builder.GetModel();

        entityModel.QueryExpression = queryExpression;

        // Key設定
        if (schema.KeyProperties.Length > 0)
        {
            entityModel.KeyProperties = schema.KeyProperties
                .Select(m => m.PropertyInfo!)
                .Where(p => p != null)
                .ToArray();
        }

        // Topic名設定（未設定の場合）
        if (entityModel.TopicName == null)
        {
            entityModel.TopicName = schema.TopicName.ToLowerInvariant();
        }

        // Stream/Table判定
        if (schema.IsKeyless)
        {
            entityModel.SetStreamTableType(Query.Abstractions.StreamTableType.Stream);
        }
        else
        {
            entityModel.SetStreamTableType(Query.Abstractions.StreamTableType.Table);
        }

        // QuerySchemaをメタデータとして保存
        StoreQuerySchema(entityModel, schema);

        return builder;
    }

    /// <summary>
    /// IEntityBuilder<T>用のHasQuery拡張メソッド
    /// </summary>
    public static EntityModelBuilder<T> HasQuery<T, TSource>(
        this IEntityBuilder<T> builder,
        Expression<Func<IQueryable<TSource>, IQueryable<T>>> queryExpression)
        where T : class
        where TSource : class
    {
        if (builder is not EntityModelBuilder<T> concreteBuilder)
            throw new ArgumentException("Builder must be EntityModelBuilder<T>", nameof(builder));
            
        return concreteBuilder.HasQuery(queryExpression);
    }

    /// <summary>
    /// キー抽出設定付きクエリ定義
    /// </summary>
    public static EntityModelBuilder<T> HasQuery<T, TSource>(
        this EntityModelBuilder<T> builder,
        Expression<Func<IQueryable<TSource>, IQueryable<T>>> queryExpression,
        bool autoKeyExtraction = true)
        where T : class
        where TSource : class
    {
        return builder.HasQuery(queryExpression);
    }

    /// <summary>
    /// ソース型を明示したクエリ定義
    /// </summary>
    public static EntityModelBuilder<TTarget> HasQueryFrom<TSource, TTarget>(
        this EntityModelBuilder<TTarget> builder,
        Expression<Func<IQueryable<TSource>, IQueryable<TTarget>>> queryExpression)
        where TTarget : class
        where TSource : class
    {
        return builder.HasQuery(q => q.FromSource(queryExpression));
    }

    /// <summary>
    /// クエリビルダーを使用した詳細設定
    /// </summary>
    public static EntityModelBuilder<T> HasQuery<T>(
        this EntityModelBuilder<T> builder,
        Action<IQueryBuilder<T>> configureQuery)
        where T : class
    {
        var queryBuilder = new QueryBuilder<T>();
        configureQuery(queryBuilder);

        var schema = queryBuilder.GetSchema();
        if (!schema.IsValid)
        {
            throw new InvalidOperationException($"Query configuration invalid: {string.Join(", ", schema.Errors)}");
        }

        var entityModel = builder.GetModel();
        if (queryBuilder.QueryExpression != null)
        {
            entityModel.QueryExpression = queryBuilder.QueryExpression;
        }

        // スキーマ情報をEntityModelに適用
        ApplySchemaToEntityModel(entityModel, schema);

        return builder;
    }

    /// <summary>
    /// EntityModelにQuerySchemaを適用
    /// </summary>
    private static void ApplySchemaToEntityModel(EntityModel entityModel, QuerySchema schema)
    {
        // Key設定
        if (schema.KeyProperties.Length > 0)
        {
            entityModel.KeyProperties = schema.KeyProperties
                .Select(m => m.PropertyInfo!)
                .Where(p => p != null)
                .ToArray();
        }

        // Topic名設定
        if (entityModel.TopicName == null && !string.IsNullOrEmpty(schema.TopicName))
        {
            entityModel.TopicName = schema.TopicName.ToLowerInvariant();
        }

        // Stream/Table設定
        var streamTableType = schema.IsKeyless
            ? Query.Abstractions.StreamTableType.Stream
            : Query.Abstractions.StreamTableType.Table;

        // Override to Stream when stream-only aggregates are used
        if (schema.UsesStreamOnlyAggregates)
        {
            if (entityModel.GetExplicitStreamTableType() == Query.Abstractions.StreamTableType.Table)
            {
                throw new InvalidOperationException("ksqlDBの仕様上、これらの集計関数はTableで利用できません");
            }

            streamTableType = Query.Abstractions.StreamTableType.Stream;
        }

        entityModel.SetStreamTableType(streamTableType);

        StoreQuerySchema(entityModel, schema);
    }

    /// <summary>
    /// QuerySchemaをEntityModelのメタデータとして保存
    /// </summary>
    private static void StoreQuerySchema(EntityModel entityModel, QuerySchema schema)
    {
        // ValidationResultのWarningsにQuerySchema情報を保存
        // （既存のEntityModel構造を変更せずに情報を保持）
        entityModel.ValidationResult ??= new ValidationResult { IsValid = true, Warnings = new() };
        
        var schemaInfo =
            $"QuerySchema:Source={schema.SourceType.FullName}," +
            $"Target={schema.TargetType.FullName}," +
            $"KeyClass={schema.KeyInfo.ClassName},KeyNs={schema.KeyInfo.Namespace}," +
            $"ValueClass={schema.ValueInfo.ClassName},ValueNs={schema.ValueInfo.Namespace}," +
            $"Keys={schema.KeyProperties.Length},Type={schema.GetStreamTableType()}," +
            $"Mode={schema.ExecutionMode}";
        
        entityModel.ValidationResult.Warnings.Add(schemaInfo);
    }
}

