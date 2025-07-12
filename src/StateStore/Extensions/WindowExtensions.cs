using Kafka.Ksql.Linq.Configuration;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.StateStore.Management;
using System.Collections.Generic;

namespace Kafka.Ksql.Linq.StateStore.Extensions;

internal static class WindowExtensions
{
    private static readonly Dictionary<IKsqlContext, IStateStoreManager> _storeManagers = new();
    private static readonly object _lock = new();

    internal static IWindowedEntitySet<T> Window<T>(this IEntitySet<T> entitySet, int windowMinutes)
        where T : class
    {
        var context = entitySet.GetContext();
        var entityModel = entitySet.GetEntityModel();
        var storeManager = GetOrCreateStateStoreManager(context);

        return new WindowedEntitySet<T>(entitySet, windowMinutes, storeManager, entityModel);
    }

    private static IStateStoreManager GetOrCreateStateStoreManager(IKsqlContext context)
    {
        lock (_lock)
        {
            if (!_storeManagers.TryGetValue(context, out var manager))
            {
                var options = Microsoft.Extensions.Options.Options.Create(new KsqlDslOptions());
                manager = new StateStoreManager(options);
                _storeManagers[context] = manager;
            }
            return manager;
        }
    }
}
