using Kafka.Ksql.Linq.Configuration;
using Kafka.Ksql.Linq.StateStore.Configuration;
using Kafka.Ksql.Linq.StateStore.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Kafka.Ksql.Linq.StateStore.Management;
internal class StateStoreManager : IStateStoreManager
{
    private readonly KsqlDslOptions _options;
    private readonly ILogger<StateStoreManager> _logger;
    private readonly ILoggerFactory? _loggerFactory;
    private readonly ConcurrentDictionary<string, object> _stores = new();
    private readonly object _lock = new();

    internal StateStoreManager(
        IOptions<KsqlDslOptions> options,
        ILoggerFactory? loggerFactory = null)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _loggerFactory = loggerFactory;
        _logger = loggerFactory?.CreateLogger<StateStoreManager>()
                 ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<StateStoreManager>.Instance;
    }

    IStateStore<TKey, TValue> IStateStoreManager.GetOrCreateStore<TKey, TValue>(
        Type entityType,
        int windowMinutes)
    {
        return GetOrCreateStoreInternal<TKey, TValue>(entityType, windowMinutes);
    }

    internal IStateStore<TKey, TValue> GetOrCreateStore<TKey, TValue>(
        Type entityType,
        int windowMinutes = 0)
        where TKey : notnull
        where TValue : class
    {
        return GetOrCreateStoreInternal<TKey, TValue>(entityType, windowMinutes);
    }

    private IStateStore<TKey, TValue> GetOrCreateStoreInternal<TKey, TValue>(
        Type entityType,
        int windowMinutes = 0)
        where TKey : notnull
        where TValue : class
    {
        var storeKey = GenerateStoreKey(entityType, windowMinutes);

        if (_stores.TryGetValue(storeKey, out var existingStore))
        {
            return (IStateStore<TKey, TValue>)existingStore;
        }

        lock (_lock)
        {
            if (_stores.TryGetValue(storeKey, out existingStore))
            {
                return (IStateStore<TKey, TValue>)existingStore;
            }

            var config = GetEntityConfiguration(entityType);
            var storeName = GenerateStoreName(entityType, windowMinutes, config?.StoreName);
            var storeOptions = CreateStoreOptions(config);

            var store = new RocksDbStateStore<TKey, TValue>(storeName, storeOptions, _loggerFactory);

            _stores.TryAdd(storeKey, store);

            _logger.LogInformation("Created StateStore: {StoreName} for entity {EntityType} window {Window}min",
                storeName, entityType.Name, windowMinutes);

            return store;
        }
    }

    internal void InitializeStoresForEntity(Type entityType)
    {
        var config = GetEntityConfiguration(entityType);
        if (config == null || config.StoreType != StoreTypes.RocksDb) return;

        if (config.Windows.Count == 0)
        {
            // デフォルトストア（Windowなし）
            GetOrCreateStoreInternal<string, object>(entityType, 0);
        }
        else
        {
            // 各Window用のストア
            foreach (var window in config.Windows)
            {
                GetOrCreateStoreInternal<string, object>(entityType, window);
                _logger.LogDebug("Initialized store for {EntityType} with {Window}min window",
                    entityType.Name, window);
            }
        }
    }

    private EntityConfiguration? GetEntityConfiguration(Type entityType)
    {
        return _options.Entities?.FirstOrDefault(e =>
            string.Equals(e.Entity, entityType.Name, StringComparison.OrdinalIgnoreCase));
    }

    private string GenerateStoreKey(Type entityType, int windowMinutes)
    {
        return $"{entityType.FullName}_{windowMinutes}min";
    }

    private string GenerateStoreName(Type entityType, int windowMinutes, string? customName)
    {
        if (!string.IsNullOrEmpty(customName))
        {
            return customName;
        }

        var baseName = entityType.Name;
        return windowMinutes > 0
            ? $"{baseName}_{windowMinutes}min_Store"
            : $"{baseName}_Store";
    }

    private StateStoreOptions CreateStoreOptions(EntityConfiguration? config)
    {
        return new StateStoreOptions
        {
            StoreType = config?.StoreType ?? StoreTypes.RocksDb,
            EnableCache = config?.EnableCache ?? false,
            Windows = config?.Windows ?? new List<int>(),
            BaseDirectory = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "ksqldsl_stores")
        };
    }

    internal List<string> GetAllStoreNames()
    {
        return _stores.Keys.ToList();
    }

    internal void CleanupStores()
    {
        _logger.LogInformation("Cleaning up {StoreCount} StateStores", _stores.Count);

        foreach (var kvp in _stores)
        {
            try
            {
                if (kvp.Value is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing store: {StoreKey}", kvp.Key);
            }
        }

        _stores.Clear();
        _logger.LogInformation("StateStore cleanup completed");
    }

    void IDisposable.Dispose()
    {
        CleanupStores();
    }
}

internal interface IStateStoreManager : IDisposable
{
    IStateStore<TKey, TValue> GetOrCreateStore<TKey, TValue>(Type entityType, int windowMinutes = 0)
        where TKey : notnull
        where TValue : class;
}
