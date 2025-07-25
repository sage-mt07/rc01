using Kafka.Ksql.Linq.Cache.Extensions;
using Kafka.Ksql.Linq.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Kafka.Ksql.Linq.Cache.Core;

internal class ReadCachedEntitySet<T> : IEntitySet<T> where T : class
{
    private readonly IKsqlContext _context;
    private readonly EntityModel _model;
    private readonly ILogger<ReadCachedEntitySet<T>> _logger;
    private readonly IEntitySet<T> _baseSet;

    internal ReadCachedEntitySet(IKsqlContext context, EntityModel model, ILoggerFactory? loggerFactory = null, IEntitySet<T>? baseSet = null)
    {
        _context = context;
        _model = model;
        _logger = loggerFactory?.CreateLogger<ReadCachedEntitySet<T>>() ?? NullLogger<ReadCachedEntitySet<T>>.Instance;
        _baseSet = baseSet ?? context.Set<T>();
    }

    public async Task<List<T>> ToListAsync(CancellationToken cancellationToken = default)
    {
        var cache = _context.GetTableCache<T>();
        if (cache == null)
        {
            _logger.LogWarning("Table cache not available for {Entity}", typeof(T).Name);
            return new List<T>();
        }

        if (!cache.IsRunning)
        {
            throw new InvalidOperationException($"Cache for {typeof(T).Name} is not running");
        }

        var all = cache.GetAll().Select(kv => kv.Value).Where(v => v != null).ToList();
        return await Task.FromResult(all);
    }

    public Task AddAsync(T entity, Dictionary<string, string>? headers = null, CancellationToken cancellationToken = default)
    {
        return _baseSet.AddAsync(entity, headers, cancellationToken);
    }

    public Task RemoveAsync(T entity, CancellationToken cancellationToken = default)
    {
        return _baseSet.RemoveAsync(entity, cancellationToken);
    }

    public Task ForEachAsync(Func<T, Task> action, TimeSpan timeout = default, CancellationToken cancellationToken = default)
    {
        return _baseSet.ForEachAsync(action, timeout, cancellationToken);
    }

    public Task ForEachAsync(Func<T, KafkaMessageContext, Task> action, TimeSpan timeout = default, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("dummy");
    }

    public string GetTopicName() => (_model.TopicName ?? typeof(T).Name).ToLowerInvariant();
    public EntityModel GetEntityModel() => _model;
    public IKsqlContext GetContext() => _context;

    public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        foreach (var item in await ToListAsync(cancellationToken))
            yield return item;
    }

    public Task ForEachAsync(Func<T, KafkaMessage<T, object>, Task> action, TimeSpan timeout = default, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
