using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.StateStore.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Kafka.Ksql.Linq.StateStore.Extensions;

/// <summary>
/// RocksDBにキャッシュされたウィンドウ確定データ読み取り用セット
/// </summary>
internal class ReadCachedWindowSet<T> : Kafka.Ksql.Linq.StateStore.IWindowedEntitySet<T> where T : class
{
    private readonly Kafka.Ksql.Linq.StateStore.IWindowedEntitySet<T> _baseSet;
    private readonly IStateStore<string, T> _stateStore;

    internal ReadCachedWindowSet(Kafka.Ksql.Linq.StateStore.IWindowedEntitySet<T> baseSet, IStateStore<string, T> stateStore)
    {
        _baseSet = baseSet ?? throw new ArgumentNullException(nameof(baseSet));
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
    }

    public int WindowMinutes => _baseSet.WindowMinutes;

    public string GetWindowTableName() => _baseSet.GetWindowTableName();

    public async Task AddAsync(T entity, CancellationToken cancellationToken = default)
        => await _baseSet.AddAsync(entity, cancellationToken);

    public async Task<List<T>> ToListAsync(CancellationToken cancellationToken = default)
    {
        var storeData = _stateStore.All().Select(kv => kv.Value).ToList();
        if (storeData.Count > 0)
            return storeData;

        return await _baseSet.ToListAsync(cancellationToken);
    }

    public async Task ForEachAsync(Func<T, Task> action, TimeSpan timeout = default, CancellationToken cancellationToken = default)
    {
        foreach (var kv in _stateStore.All())
        {
            await action(kv.Value);
        }
    }

    public string GetTopicName()
    {
        var model = _baseSet.GetEntityModel();
        var baseName = (model.TopicName ?? _baseSet.GetTopicName()).ToLowerInvariant();
        return $"{baseName}_window_{WindowMinutes}_final";
    }

    public EntityModel GetEntityModel() => _baseSet.GetEntityModel();

    public IStateStore<string, T> GetStateStore() => _stateStore;

    public IKsqlContext GetContext() => _baseSet.GetContext();

    public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        foreach (var kv in _stateStore.All())
        {
            yield return kv.Value;
        }
        await Task.CompletedTask;
    }
}
