using Kafka.Ksql.Linq.Core.Abstractions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Kafka.Ksql.Linq.Core.Window;

internal class WindowCollection<T> : IWindowCollection<T> where T : class
{
    private readonly IEntitySet<T> _baseEntitySet;
    private readonly ConcurrentDictionary<int, IWindowedEntitySet<T>> _windows;

    public int[] WindowSizes { get; }

    internal WindowCollection(IEntitySet<T> baseEntitySet, int[] windowSizes)
    {
        _baseEntitySet = baseEntitySet ?? throw new ArgumentNullException(nameof(baseEntitySet));
        WindowSizes = windowSizes?.Distinct().OrderBy(x => x).ToArray() ??
                     throw new ArgumentNullException(nameof(windowSizes));

        _windows = new ConcurrentDictionary<int, IWindowedEntitySet<T>>();
    }

    public IWindowedEntitySet<T> this[int windowMinutes]
    {
        get
        {
            if (!WindowSizes.Contains(windowMinutes))
            {
                throw new ArgumentException(
                    $"Window size {windowMinutes} is not configured. Available sizes: {string.Join(", ", WindowSizes)}");
            }

            return _windows.GetOrAdd(windowMinutes,
                size => new WindowedEntitySet<T>(_baseEntitySet, size));
        }
    }

    public async Task<Dictionary<int, List<T>>> GetAllWindowsAsync(CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<int, List<T>>();

        var tasks = WindowSizes.Select(async windowSize =>
        {
            var windowedEntitySet = this[windowSize];
            var data = await windowedEntitySet.ToListAsync(cancellationToken);
            return new { WindowSize = windowSize, Data = data };
        });

        var completedTasks = await Task.WhenAll(tasks);

        foreach (var result in completedTasks)
        {
            results[result.WindowSize] = result.Data;
        }

        return results;
    }
}
