using Kafka.Ksql.Linq.Configuration;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.StateStore.Management;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;

namespace Kafka.Ksql.Linq.StateStore.Extensions;

internal static class KafkaContextStateStoreExtensions
{
    private static readonly Dictionary<IKsqlContext, IStateStoreManager> _contextStoreManagers = new();
    private static readonly object _lock = new();

    internal static void InitializeStateStores(this IKsqlContext context, KsqlDslOptions options)
    {
        lock (_lock)
        {
            if (_contextStoreManagers.ContainsKey(context))
                return;

            var optionsWrapper = Options.Create(options);
            var storeManager = new StateStoreManager(optionsWrapper);

            // Entities設定に基づいてStateStoreを初期化
            var entityModels = context.GetEntityModels();
            foreach (var entityModel in entityModels.Values)
            {
                var entityConfig = options.Entities?.Find(e =>
                    string.Equals(e.Entity, entityModel.EntityType.Name, StringComparison.OrdinalIgnoreCase));

                if (entityConfig?.StoreType == StoreTypes.RocksDb)
                {
                    ((StateStoreManager)storeManager).InitializeStoresForEntity(entityModel.EntityType);
                }
            }

            _contextStoreManagers[context] = storeManager;
        }
    }

    internal static IStateStoreManager? GetStateStoreManager(this IKsqlContext context)
    {
        lock (_lock)
        {
            return _contextStoreManagers.TryGetValue(context, out var manager) ? manager : null;
        }
    }

    internal static void CleanupStateStores(this IKsqlContext context)
    {
        lock (_lock)
        {
            if (_contextStoreManagers.TryGetValue(context, out var manager))
            {
                manager.Dispose();
                _contextStoreManagers.Remove(context);
            }
        }
    }
}



