using Kafka.Ksql.Linq.Cache.Configuration;
using Kafka.Ksql.Linq.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;

namespace Kafka.Ksql.Linq.Cache.Core;

internal class TableCacheRegistry : IDisposable
{
    private readonly Dictionary<Type, object> _caches = new();
    private readonly ILogger<TableCacheRegistry> _logger;

    public TableCacheRegistry(ILoggerFactory? loggerFactory = null)
    {
        _logger = loggerFactory?.CreateLogger<TableCacheRegistry>()
                 ?? NullLogger<TableCacheRegistry>.Instance;
    }

    public void InitializeCaches(IEnumerable<EntityModel> models, TableCacheOptions options, ILoggerFactory? loggerFactory = null)
    {
        foreach (var model in models)
        {
            var config = options.Entries.Find(e => string.Equals(e.Entity, model.EntityType.Name, StringComparison.OrdinalIgnoreCase));
            if (config == null || !config.EnableCache)
                continue;

            var cacheType = typeof(RocksDbTableCache<>).MakeGenericType(model.EntityType);
            var cache = (ITableCache<object>)Activator.CreateInstance(cacheType, loggerFactory)!;
            cache.InitializeAsync().GetAwaiter().GetResult();
            _caches[model.EntityType] = cache;
            _logger.LogInformation("Initialized cache for {Entity}", model.EntityType.Name);
        }
    }

    public ITableCache<T>? GetCache<T>() where T : class
    {
        if (_caches.TryGetValue(typeof(T), out var cache) && cache is ITableCache<T> typed)
            return typed;
        return null;
    }

    public void Dispose()
    {
        foreach (var cache in _caches.Values)
        {
            if (cache is IDisposable d)
                d.Dispose();
        }
        _caches.Clear();
    }
}
