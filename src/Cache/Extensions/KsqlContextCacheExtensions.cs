using Kafka.Ksql.Linq.Cache.Configuration;
using Kafka.Ksql.Linq.Cache.Core;
using Kafka.Ksql.Linq.Configuration;
using Kafka.Ksql.Linq.Core.Abstractions;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace Kafka.Ksql.Linq.Cache.Extensions;

internal static class KsqlContextCacheExtensions
{
    private static readonly Dictionary<IKsqlContext, TableCacheRegistry> _registries = new();
    private static readonly object _lock = new();

    internal static void UseTableCache(this IKsqlContext context, KsqlDslOptions options, ILoggerFactory? loggerFactory = null)
    {
        lock (_lock)
        {
            if (_registries.ContainsKey(context))
                return;

            var registry = new TableCacheRegistry(loggerFactory);
            var cacheOptions = new TableCacheOptions();
            foreach (var e in options.Entities)
            {
                cacheOptions.Entries.Add(new TableCacheEntry
                {
                    Entity = e.Entity,
                    SourceTopic = e.SourceTopic,
                    StoreType = e.StoreType,
                    EnableCache = e.EnableCache,
                    StoreName = e.StoreName,
                    BaseDirectory = null
                });
            }
            registry.InitializeCaches(context.GetEntityModels().Values, cacheOptions, loggerFactory);
            _registries[context] = registry;
        }
    }

    internal static TableCacheRegistry? GetTableCacheRegistry(this IKsqlContext context)
    {
        lock (_lock)
        {
            return _registries.TryGetValue(context, out var reg) ? reg : null;
        }
    }

    internal static ITableCache<T>? GetTableCache<T>(this IKsqlContext context) where T : class
    {
        var reg = context.GetTableCacheRegistry();
        return reg?.GetCache<T>();
    }
}
