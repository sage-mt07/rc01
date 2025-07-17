using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kafka.Ksql.Linq.Cache.Core;

internal class RocksDbTableCache<T> : ITableCache<T> where T : class
{
    private readonly ConcurrentDictionary<string, T> _store = new();
    private readonly ILogger<RocksDbTableCache<T>> _logger;
    private volatile bool _running = false;

    public bool IsRunning => _running;

    public RocksDbTableCache(ILoggerFactory? loggerFactory = null)
    {
        _logger = loggerFactory?.CreateLogger<RocksDbTableCache<T>>()
                 ?? NullLogger<RocksDbTableCache<T>>.Instance;
    }

    public Task InitializeAsync()
    {
        // Simulate Streamiz RUNNING state immediately
        _running = true;
        _logger.LogInformation("Table cache for {Type} is RUNNING", typeof(T).Name);
        return Task.CompletedTask;
    }

    public bool TryGet(string key, out T? value)
    {
        return _store.TryGetValue(key, out value);
    }

    public IEnumerable<KeyValuePair<string, T>> GetAll()
    {
        return _store.ToArray();
    }


    public void Dispose()
    {
        _store.Clear();
        _running = false;
        _logger.LogInformation("Table cache for {Type} disposed", typeof(T).Name);
    }
}
