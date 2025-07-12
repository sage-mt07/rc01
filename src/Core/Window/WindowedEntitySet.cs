using Kafka.Ksql.Linq.Core.Abstractions;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Kafka.Ksql.Linq.Core.Window;


internal class WindowedEntitySet<T> : IWindowedEntitySet<T> where T : class
{
    private readonly IEntitySet<T> _baseEntitySet;
    private readonly int _windowMinutes;
    private readonly WindowAggregationConfig _windowConfig;
    private readonly PropertyInfo? _timestampProperty;

    public int WindowMinutes => _windowMinutes;

    internal WindowedEntitySet(IEntitySet<T> baseEntitySet, int windowMinutes)
    {
        _baseEntitySet = baseEntitySet ?? throw new ArgumentNullException(nameof(baseEntitySet));
        _windowMinutes = windowMinutes;

        _windowConfig = new WindowAggregationConfig
        {
            WindowSize = TimeSpan.FromMinutes(windowMinutes),
            GracePeriod = TimeSpan.FromSeconds(3)
        };

        _timestampProperty = ValidateAndGetTimestampProperty();
    }

    /// <summary>
    /// タイムスタンプとして使用できるプロパティを検証・取得
    /// </summary>
    private PropertyInfo? ValidateAndGetTimestampProperty()
    {
        var entityType = typeof(T);
        var timestampProperties = entityType.GetProperties()
            .Where(p => p.PropertyType == typeof(DateTime) || p.PropertyType == typeof(DateTimeOffset))
            .ToArray();

        if (timestampProperties.Length == 0)
        {
            throw new InvalidOperationException(
                $"Entity {entityType.Name} must have exactly one DateTime or DateTimeOffset property to use as the event timestamp");
        }

        if (timestampProperties.Length > 1)
        {
            throw new InvalidOperationException(
                $"Entity {entityType.Name} has multiple timestamp-capable properties. Only one is allowed for window operations.");
        }

        var timestampProperty = timestampProperties[0];

        // DateTime型であることを確認
        if (timestampProperty.PropertyType != typeof(DateTime) &&
            timestampProperty.PropertyType != typeof(DateTime?) &&
            timestampProperty.PropertyType != typeof(DateTimeOffset) &&
            timestampProperty.PropertyType != typeof(DateTimeOffset?))
        {
            throw new InvalidOperationException(
                $"Property {timestampProperty.Name} used as timestamp must be DateTime or DateTimeOffset type");
        }

        return timestampProperty;
    }

    public IEntitySet<TResult> Aggregate<TResult>(
        Expression<Func<IGrouping<object, T>, TResult>> aggregationExpression,
        TimeSpan? gracePeriod = null) where TResult : class
    {
        // デフォルトのグループ化（全体を一つのグループとする）
        Expression<Func<T, object>> defaultGroupBy = x => "ALL";
        return GroupByAggregate(defaultGroupBy, aggregationExpression, gracePeriod);
    }

    public IEntitySet<TResult> GroupByAggregate<TKey, TResult>(
        Expression<Func<T, TKey>> groupByExpression,
        Expression<Func<IGrouping<TKey, T>, TResult>> aggregationExpression,
        TimeSpan? gracePeriod = null) where TResult : class
    {
        var config = _windowConfig with { GracePeriod = gracePeriod ?? _windowConfig.GracePeriod };

        // WindowAggregatedEntitySetは直接IEntitySet<TResult>を実装
        return new WindowAggregatedEntitySet<T, TKey, TResult>(
            _baseEntitySet,
            _windowMinutes,
            groupByExpression,
            aggregationExpression,
            config);
    }

    public string GetWindowTableName()
    {
        var baseTopicName = _baseEntitySet.GetTopicName();
        return $"{baseTopicName}_WINDOW_{_windowMinutes}MIN";
    }

    // ✅ IEntitySet<T> インターフェース実装 - ベースEntitySetに委譲
    public async Task AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        await _baseEntitySet.AddAsync(entity, cancellationToken);
    }

    public async Task<List<T>> ToListAsync(CancellationToken cancellationToken = default)
    {
        // ウィンドウクエリは集約前の生データは通常取得しない
        // 必要に応じてベースEntitySetに委譲
        return await _baseEntitySet.ToListAsync(cancellationToken);
    }

    public async Task ForEachAsync(Func<T, Task> action, TimeSpan timeout = default, CancellationToken cancellationToken = default)
    {
        await _baseEntitySet.ForEachAsync(action, timeout, cancellationToken);
    }

    public string GetTopicName() => _baseEntitySet.GetTopicName();

    public EntityModel GetEntityModel() => _baseEntitySet.GetEntityModel();

    public IKsqlContext GetContext() => _baseEntitySet.GetContext();

    // ✅ IAsyncEnumerable<T> インターフェース実装
    public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        await foreach (var item in _baseEntitySet.WithCancellation(cancellationToken))
        {
            yield return item;
        }
    }

    public override string ToString()
    {
        return $"WindowedEntitySet<{typeof(T).Name}> - {_windowMinutes}min window → {GetWindowTableName()}";
    }
}
