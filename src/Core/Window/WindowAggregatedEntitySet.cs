using Kafka.Ksql.Linq.Core.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Kafka.Ksql.Linq.Query.Abstractions;
using System.Threading;
using System.Threading.Tasks;

namespace Kafka.Ksql.Linq.Core.Window;

internal class WindowAggregatedEntitySet<TSource, TKey, TResult> : IEntitySet<TResult>
    where TSource : class
    where TResult : class
{
    private readonly IEntitySet<TSource> _sourceEntitySet;
    private readonly int _windowMinutes;
    private readonly Expression<Func<TSource, TKey>> _groupByExpression;
    private readonly Expression<Func<IGrouping<TKey, TSource>, TResult>> _aggregationExpression;
    private readonly WindowAggregationConfig _config;
    private readonly string _windowTableName;
    private readonly EntityModel _resultEntityModel;

    internal WindowAggregatedEntitySet(
        IEntitySet<TSource> sourceEntitySet,
        int windowMinutes,
        Expression<Func<TSource, TKey>> groupByExpression,
        Expression<Func<IGrouping<TKey, TSource>, TResult>> aggregationExpression,
        WindowAggregationConfig config)
    {
        _sourceEntitySet = sourceEntitySet ?? throw new ArgumentNullException(nameof(sourceEntitySet));
        _windowMinutes = windowMinutes;
        _groupByExpression = groupByExpression ?? throw new ArgumentNullException(nameof(groupByExpression));
        _aggregationExpression = aggregationExpression ?? throw new ArgumentNullException(nameof(aggregationExpression));
        _config = config ?? throw new ArgumentNullException(nameof(config));

        _windowTableName = GenerateWindowTableName();
        _resultEntityModel = CreateResultEntityModel<TResult>();
    }

    private string GenerateWindowTableName()
    {
        var sourceTopicName = _sourceEntitySet.GetTopicName();
        var hash = Math.Abs((_groupByExpression.ToString() + _aggregationExpression.ToString()).GetHashCode()) % 10000;
        return $"{sourceTopicName}_WINDOW_{_windowMinutes}MIN_AGG_{hash}";
    }

    private static EntityModel CreateResultEntityModel<T>() where T : class
    {
        var model = new EntityModel
        {
            EntityType = typeof(T),
            TopicName = $"{typeof(T).Name.ToLowerInvariant()}_windowresult",
            AllProperties = typeof(T).GetProperties(),
            KeyProperties = Array.Empty<PropertyInfo>(),
            ValidationResult = new ValidationResult { IsValid = true }
        };
        model.SetStreamTableType(StreamTableType.Table);
        return model;
    }

    // ✅ IEntitySet<TResult> インターフェース実装
    public async Task AddAsync(TResult entity, Dictionary<string, string>? headers = null, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        throw new NotSupportedException("Cannot add entities to a window aggregated result set");
    }

    public Task RemoveAsync(TResult entity, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Cannot remove entities from a window aggregated result set");
    }

    public async Task<List<TResult>> ToListAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. ウィンドウテーブルの作成（必要に応じて）
            await EnsureWindowTableExistsAsync();

            // 2. ウィンドウテーブルからのクエリ実行
            var ksqlQuery = GenerateWindowQuery();

            // TODO: 実際のKsqlDbExecutorを使用してクエリ実行
            // 現在は仮実装
            await Task.Delay(100, cancellationToken); // シミュレート

            return new List<TResult>(); // 仮の空リスト
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to execute window aggregation query for {_windowTableName}", ex);
        }
    }

    public async Task ForEachAsync(Func<TResult, Task> action, TimeSpan timeout = default, CancellationToken cancellationToken = default)
    {
        var results = await ToListAsync(cancellationToken);
        foreach (var result in results)
        {
            await action(result);
        }
    }

    public string GetTopicName() => _windowTableName;

    public EntityModel GetEntityModel() => _resultEntityModel;

    public IKsqlContext GetContext() => _sourceEntitySet.GetContext();

    // ✅ IAsyncEnumerable<TResult> インターフェース実装
    public async IAsyncEnumerator<TResult> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        var results = await ToListAsync(cancellationToken);
        foreach (var result in results)
        {
            yield return result;
        }
    }

    /// <summary>
    /// ウィンドウテーブルの存在確認・作成
    /// </summary>
    private async Task EnsureWindowTableExistsAsync()
    {
        var context = GetContext();

        // KafkaContextからDDLGeneratorを取得する方法が必要
        // 現在のアーキテクチャでは直接アクセスできないため、
        // ContextにDDLGeneratorのアクセス方法を追加する必要がある

        var createTableQuery = GenerateCreateWindowTableQuery();

        // TODO: DDLの実行
        // await ddlExecutor.ExecuteDDLAsync(createTableQuery);

        await Task.CompletedTask; // 仮実装
    }

    /// <summary>
    /// CREATE TABLE AS SELECT でウィンドウテーブルを作成するKSQL生成
    /// </summary>
    private string GenerateCreateWindowTableQuery()
    {
        var sourceTableName = _sourceEntitySet.GetTopicName();
        var timestampColumn = GetTimestampColumnName();

        // GroupBy部分の生成
        var groupByClause = GenerateGroupByClause();

        // 集約部分の生成  
        var selectClause = GenerateAggregationSelectClause();

        // ウィンドウ部分の生成
        var windowClause = GenerateWindowClause();

        var query = $@"
CREATE TABLE {_windowTableName} AS
SELECT {selectClause}
FROM {sourceTableName}
{windowClause}
{groupByClause}
EMIT {(_config.OutputMode == WindowOutputMode.Final ? "FINAL" : "CHANGES")}";

        return query.Trim();
    }

    /// <summary>
    /// プルクエリ用のSELECT文生成
    /// </summary>
    private string GenerateWindowQuery()
    {
        return $"SELECT * FROM {_windowTableName}";
    }

    private string GetTimestampColumnName()
    {
        var sourceType = typeof(TSource);
        var timestampProperty = sourceType.GetProperties()
            .FirstOrDefault(p => p.PropertyType == typeof(DateTime) || p.PropertyType == typeof(DateTimeOffset));

        if (timestampProperty == null)
        {
            throw new InvalidOperationException(
                $"No timestamp property found in {sourceType.Name}");
        }

        return timestampProperty.Name.ToUpper();
    }

    private string GenerateGroupByClause()
    {
        // Expression<Func<TSource, TKey>>からKSQL GROUP BY句を生成
        var visitor = new GroupByExpressionVisitor();
        visitor.Visit(_groupByExpression);

        var groupByColumns = visitor.GetColumns();

        if (groupByColumns.Count == 0)
        {
            return ""; // GROUP BY なし
        }

        return $"GROUP BY {string.Join(", ", groupByColumns)}";
    }

    private string GenerateAggregationSelectClause()
    {
        // Expression<Func<IGrouping<TKey, TSource>, TResult>>からSELECT句を生成
        var visitor = new AggregationExpressionVisitor();
        visitor.Visit(_aggregationExpression);

        var selectColumns = visitor.GetSelectColumns();

        if (selectColumns.Count == 0)
        {
            return "*";
        }

        return string.Join(", ", selectColumns);
    }

    private string GenerateWindowClause()
    {
        var windowSizeMinutes = _config.WindowSize.TotalMinutes;
        var gracePeriodSeconds = _config.GracePeriod.TotalSeconds;

        return _config.WindowType switch
        {
            WindowType.Tumbling =>
                $"WINDOW TUMBLING (SIZE {windowSizeMinutes} MINUTES, GRACE PERIOD {gracePeriodSeconds} SECONDS)",
            WindowType.Hopping =>
                $"WINDOW HOPPING (SIZE {windowSizeMinutes} MINUTES, ADVANCE BY {windowSizeMinutes / 2} MINUTES, GRACE PERIOD {gracePeriodSeconds} SECONDS)",
            WindowType.Session =>
                $"WINDOW SESSION (GAP {windowSizeMinutes} MINUTES)",
            _ => throw new NotSupportedException($"Window type {_config.WindowType} is not supported")
        };
    }

    public override string ToString()
    {
        return $"WindowAggregatedEntitySet<{typeof(TResult).Name}> - {_windowMinutes}min → {_windowTableName}";
    }
}

// 式木解析用のVisitorクラス群
internal class GroupByExpressionVisitor : ExpressionVisitor
{
    private readonly List<string> _columns = new();

    public List<string> GetColumns() => _columns;

    protected override Expression VisitMember(MemberExpression node)
    {
        _columns.Add(node.Member.Name.ToUpper());
        return base.VisitMember(node);
    }

    protected override Expression VisitNew(NewExpression node)
    {
        foreach (var arg in node.Arguments)
        {
            if (arg is MemberExpression member)
            {
                _columns.Add(member.Member.Name.ToUpper());
            }
        }
        return base.VisitNew(node);
    }
}

internal class AggregationExpressionVisitor : ExpressionVisitor
{
    private readonly List<string> _selectColumns = new();

    public List<string> GetSelectColumns() => _selectColumns;

    protected override Expression VisitNew(NewExpression node)
    {
        for (int i = 0; i < node.Arguments.Count; i++)
        {
            var arg = node.Arguments[i];
            var alias = node.Members?[i]?.Name ?? $"col{i}";

            var columnExpr = GenerateColumnExpression(arg);
            _selectColumns.Add($"{columnExpr} AS {alias}");
        }

        return base.VisitNew(node);
    }

    private string GenerateColumnExpression(Expression expression)
    {
        return expression switch
        {
            MethodCallExpression methodCall => GenerateAggregationFunction(methodCall),
            MemberExpression member => member.Member.Name.ToUpper(),
            _ => "UNKNOWN"
        };
    }

    private string GenerateAggregationFunction(MethodCallExpression methodCall)
    {
        var methodName = methodCall.Method.Name.ToUpper();

        return methodName switch
        {
            "SUM" => GenerateFunctionCall("SUM", methodCall),
            "COUNT" => "COUNT(*)",
            "MAX" => GenerateFunctionCall("MAX", methodCall),
            "MIN" => GenerateFunctionCall("MIN", methodCall),
            "LATESTBYOFFSET" => GenerateFunctionCall("LATEST_BY_OFFSET", methodCall),
            "EARLIESTBYOFFSET" => GenerateFunctionCall("EARLIEST_BY_OFFSET", methodCall),
            "AVERAGE" => GenerateFunctionCall("AVG", methodCall),
            _ => $"{methodName}(UNKNOWN)"
        };
    }

    private string GenerateFunctionCall(string functionName, MethodCallExpression methodCall)
    {
        // g.Sum(x => x.Property) のようなラムダ式から Property を抽出
        if (methodCall.Arguments.Count > 0 &&
            methodCall.Arguments[0] is LambdaExpression lambda &&
            lambda.Body is MemberExpression member)
        {
            return $"{functionName}({member.Member.Name.ToUpper()})";
        }

        return $"{functionName}(*)";
    }
}
