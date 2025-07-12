using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.StateStore.Core;
using Kafka.Ksql.Linq.StateStore.Management;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Kafka.Ksql.Linq.StateStore;

internal class WindowedEntitySet<T> : IWindowedEntitySet<T> where T : class
{
    private readonly IEntitySet<T> _baseEntitySet;
    private readonly int _windowMinutes;
    private readonly StateStoreManager _storeManager;
    private readonly EntityModel _entityModel;
    private readonly IStateStore<string, T> _stateStore;

    internal WindowedEntitySet(
        IEntitySet<T> baseEntitySet,
        int windowMinutes,
        StateStoreManager storeManager,
        EntityModel entityModel)
    {
        _baseEntitySet = baseEntitySet ?? throw new ArgumentNullException(nameof(baseEntitySet));
        _windowMinutes = windowMinutes;
        _storeManager = storeManager ?? throw new ArgumentNullException(nameof(storeManager));
        _entityModel = entityModel ?? throw new ArgumentNullException(nameof(entityModel));

        _stateStore = _storeManager.GetOrCreateStore<string, T>(_entityModel.EntityType, windowMinutes);
    }

    public int WindowMinutes => _windowMinutes;

    public IStateStore<string, T> GetStateStore() => _stateStore;

    public string GetWindowTableName()
    {
        var baseTopicName = _baseEntitySet.GetTopicName();
        return $"{baseTopicName}_WINDOW_{_windowMinutes}MIN";
    }

    // IEntitySet<T> 実装を委譲
    public async System.Threading.Tasks.Task AddAsync(T entity, System.Threading.CancellationToken cancellationToken = default)
    {
        // StateStoreに保存
        var key = GenerateKey(entity);
        _stateStore.Put(key, entity);

        // 元のEntitySetにも追加
        await _baseEntitySet.AddAsync(entity, cancellationToken);
    }

    public async System.Threading.Tasks.Task<List<T>> ToListAsync(System.Threading.CancellationToken cancellationToken = default)
    {
        // StateStoreから取得
        var storeData = _stateStore.All().Select(kvp => kvp.Value).ToList();

        // ベースからも取得してマージ
        var baseData = await _baseEntitySet.ToListAsync(cancellationToken);

        // 重複を除去して返す
        return storeData.Concat(baseData).Distinct().ToList();
    }

    public async System.Threading.Tasks.Task ForEachAsync(Func<T, System.Threading.Tasks.Task> action, TimeSpan timeout = default, System.Threading.CancellationToken cancellationToken = default)
    {
        await _baseEntitySet.ForEachAsync(action, timeout, cancellationToken);
    }

    public string GetTopicName() => _baseEntitySet.GetTopicName();

    public EntityModel GetEntityModel() => _entityModel;

    public IKsqlContext GetContext() => _baseEntitySet.GetContext();

    public async System.Collections.Generic.IAsyncEnumerator<T> GetAsyncEnumerator(System.Threading.CancellationToken cancellationToken = default)
    {
        await foreach (var item in _baseEntitySet.WithCancellation(cancellationToken))
        {
            yield return item;
        }
    }

    private string GenerateKey(T entity)
    {
        if (entity == null) return Guid.NewGuid().ToString();

        // キープロパティからキー生成
        var keyProperties = _entityModel.KeyProperties;
        if (keyProperties.Length == 0)
        {
            return entity.GetHashCode().ToString();
        }

        if (keyProperties.Length == 1)
        {
            var value = keyProperties[0].GetValue(entity);
            return value?.ToString() ?? Guid.NewGuid().ToString();
        }

        // 複合キー
        var keyParts = keyProperties.Select(p => p.GetValue(entity)?.ToString() ?? "null");
        return string.Join("|", keyParts);
    }
}
