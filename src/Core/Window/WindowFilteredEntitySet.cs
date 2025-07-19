using Kafka.Ksql.Linq.Core.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Kafka.Ksql.Linq.Core.Window;

internal class WindowFilteredEntitySet<T> : IEntitySet<T> where T : class
{
    private readonly IEntitySet<T> _baseSet;
    private readonly int _windowMinutes;
    private readonly PropertyInfo _windowProp;

    internal WindowFilteredEntitySet(IEntitySet<T> baseSet, int windowMinutes)
    {
        _baseSet = baseSet ?? throw new ArgumentNullException(nameof(baseSet));
        if (windowMinutes <= 0)
            throw new ArgumentException("Window minutes must be positive", nameof(windowMinutes));
        _windowMinutes = windowMinutes;

        _windowProp = baseSet.GetEntityModel().AllProperties
            .FirstOrDefault(p => p.Name == "WindowMinutes" &&
                                 (p.PropertyType == typeof(int) || p.PropertyType == typeof(int?)))
            ?? throw new InvalidOperationException($"Entity {typeof(T).Name} does not contain WindowMinutes property");
    }

    private bool Matches(T entity)
    {
        var value = _windowProp.GetValue(entity);
        if (value == null) return false;
        return Convert.ToInt32(value) == _windowMinutes;
    }

    public async Task<List<T>> ToListAsync(CancellationToken cancellationToken = default)
    {
        var all = await _baseSet.ToListAsync(cancellationToken);
        return all.Where(Matches).ToList();
    }

    public async Task ForEachAsync(Func<T, Task> action, TimeSpan timeout = default, CancellationToken cancellationToken = default)
    {
        await _baseSet.ForEachAsync(async e =>
        {
            if (Matches(e))
                await action(e);
        }, timeout, cancellationToken);
    }

    public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        await foreach (var item in _baseSet.WithCancellation(cancellationToken))
        {
            if (Matches(item))
                yield return item;
        }
    }

    public Task AddAsync(T entity, CancellationToken cancellationToken = default) => _baseSet.AddAsync(entity, cancellationToken);
    public string GetTopicName() => _baseSet.GetTopicName();
    public EntityModel GetEntityModel() => _baseSet.GetEntityModel();
    public IKsqlContext GetContext() => _baseSet.GetContext();
}
