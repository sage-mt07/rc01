using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.StateStore.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Kafka.Ksql.Linq.StateStore;



/// <summary>
/// StateStore機能付きのEntitySet実装
/// IEntitySet<T>を直接実装し、StateStoreへの保存機能を追加
/// </summary>
internal class EventSetWithStateStore<T> : IEntitySet<T> where T : class
{
    private readonly IKsqlContext _context;
    private readonly EntityModel _entityModel;
    private readonly IEntitySet<T> _baseEntitySet;

    internal EventSetWithStateStore(IKsqlContext context, EntityModel entityModel)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _entityModel = entityModel ?? throw new ArgumentNullException(nameof(entityModel));

        // 基底となるEntitySetを取得
        _baseEntitySet = context.Set<T>();
    }

    /// <summary>
    /// エンティティを追加（StateStore保存機能付き）
    /// </summary>
    public async Task AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        // StateStoreへの保存処理
        var storeManager = _context.GetStateStoreManager();
        if (storeManager != null)
        {
            try
            {
                var store = storeManager.GetOrCreateStore<string, T>(typeof(T), 0);
                var key = GenerateEntityKey(entity);
                store.Put(key, entity);
            }
            catch (Exception ex)
            {
                // StateStore保存失敗は警告のみ、メイン処理は継続
                System.Diagnostics.Debug.WriteLine($"StateStore save failed: {ex.Message}");
            }
        }

        // 元の送信処理に委譲
        await _baseEntitySet.AddAsync(entity, cancellationToken);
    }

    /// <summary>
    /// エンティティキー生成
    /// </summary>
    private string GenerateEntityKey(T entity)
    {
        if (entity == null)
            return Guid.NewGuid().ToString();

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
        var keyParts = new string[keyProperties.Length];
        for (int i = 0; i < keyProperties.Length; i++)
        {
            keyParts[i] = keyProperties[i].GetValue(entity)?.ToString() ?? "null";
        }

        return string.Join("|", keyParts);
    }

    // IEntitySet<T>の他のメソッドを委譲実装
    public async Task<List<T>> ToListAsync(CancellationToken cancellationToken = default)
    {
        return await _baseEntitySet.ToListAsync(cancellationToken);
    }

    public async Task ForEachAsync(Func<T, Task> action, TimeSpan timeout = default, CancellationToken cancellationToken = default)
    {
        await _baseEntitySet.ForEachAsync(action, timeout, cancellationToken);
    }

    public string GetTopicName()
    {
        return _baseEntitySet.GetTopicName();
    }

    public EntityModel GetEntityModel()
    {
        return _entityModel;
    }

    public IKsqlContext GetContext()
    {
        return _context;
    }

    public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        await foreach (var item in _baseEntitySet.WithCancellation(cancellationToken))
        {
            yield return item;
        }
    }
}

/// <summary>
/// IKsqlContext拡張メソッド（StateStore統合用）
/// </summary>
internal static class KafkaContextStateStoreIntegrationExtensions
{
    /// <summary>
    /// StateStore機能付きEntitySetを取得
    /// </summary>
    public static EventSetWithStateStore<T> SetWithStateStore<T>(this IKsqlContext context) where T : class
    {
        var entityModel = context.GetEntityModels().FirstOrDefault(x => x.Key == typeof(T)).Value;
        if (entityModel == null)
        {
            throw new InvalidOperationException($"EntityModel not found for type {typeof(T).Name}");
        }

        return new EventSetWithStateStore<T>(context, entityModel);
    }
}
