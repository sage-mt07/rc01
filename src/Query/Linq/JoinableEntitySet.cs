using Kafka.Ksql.Linq.Core.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace Kafka.Ksql.Linq.Query.Linq;

/// <summary>
/// JOIN操作対応EntitySet（document index 21の補完版）
/// 設計理由：既存実装の不足部分を補完し、新アーキテクチャとの統合を実現
/// </summary>
public class JoinableEntitySet<T> : IEntitySet<T>, IJoinableEntitySet<T> where T : class
{
    private readonly IEntitySet<T> _baseEntitySet;

    public JoinableEntitySet(IEntitySet<T> baseEntitySet)
    {
        _baseEntitySet = baseEntitySet ?? throw new ArgumentNullException(nameof(baseEntitySet));
    }

    // ✅ IEntitySet<T>の必須実装
    public async Task AddAsync(T entity, Dictionary<string, string>? headers = null, CancellationToken cancellationToken = default)
    {
        await _baseEntitySet.AddAsync(entity, headers, cancellationToken);
    }

    public async Task RemoveAsync(T entity, CancellationToken cancellationToken = default)
    {
        await _baseEntitySet.RemoveAsync(entity, cancellationToken);
    }

    public async Task<List<T>> ToListAsync(CancellationToken cancellationToken = default)
    {
        return await _baseEntitySet.ToListAsync(cancellationToken);
    }

    public async Task ForEachAsync(Func<T, Task> action, TimeSpan timeout = default, CancellationToken cancellationToken = default)
    {
        await _baseEntitySet.ForEachAsync(action, timeout, cancellationToken);
    }

    public async Task ForEachAsync(Func<T, KafkaMessageContext, Task> action, TimeSpan timeout = default, CancellationToken cancellationToken = default)
    {
        await _baseEntitySet.ForEachAsync(action, timeout, cancellationToken);
    }

    public string GetTopicName() => _baseEntitySet.GetTopicName();

    public EntityModel GetEntityModel() => _baseEntitySet.GetEntityModel();

    public IKsqlContext GetContext() => _baseEntitySet.GetContext();

    // ✅ IAsyncEnumerable<T>の実装
    public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        await foreach (var item in _baseEntitySet.WithCancellation(cancellationToken))
        {
            yield return item;
        }
    }

    // ✅ IJoinableEntitySet<T>のJOIN機能実装
    public IJoinResult<T, TInner> Join<TInner, TKey>(
        IEntitySet<TInner> inner,
        Expression<Func<T, TKey>> outerKeySelector,
        Expression<Func<TInner, TKey>> innerKeySelector) where TInner : class
    {
        if (inner == null)
            throw new ArgumentNullException(nameof(inner));
        if (outerKeySelector == null)
            throw new ArgumentNullException(nameof(outerKeySelector));
        if (innerKeySelector == null)
            throw new ArgumentNullException(nameof(innerKeySelector));

        return new JoinResult<T, TInner>(this, inner, outerKeySelector, innerKeySelector);
    }

    public override string ToString()
    {
        return $"JoinableEntitySet<{typeof(T).Name}> wrapping {_baseEntitySet}";
    }
}
