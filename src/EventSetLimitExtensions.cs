using Kafka.Ksql.Linq.Core.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Kafka.Ksql.Linq;


/// <summary>
/// Extensions for limiting the number of items in an entity set.
/// </summary>
public static class EventSetLimitExtensions
{
    /// <summary>
    /// Returns the newest <paramref name="count"/> items ordered by BarTime and removes older items when supported.
    /// </summary>
    public static async Task<List<T>> Limit<T>(this IEntitySet<T> entitySet, int count, CancellationToken cancellationToken = default) where T : class
    {
        if (entitySet == null) throw new ArgumentNullException(nameof(entitySet));
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));

        var items = await entitySet.ToListAsync(cancellationToken);
        var barTimeProp = typeof(T).GetProperty("BarTime", BindingFlags.Public | BindingFlags.Instance);
        if (barTimeProp == null)
            throw new InvalidOperationException($"Type {typeof(T).Name} must have BarTime property.");

        var ordered = items.OrderByDescending(i => (DateTime)barTimeProp.GetValue(i)!).ToList();
        var toKeep = ordered.Take(count).ToList();
        var toRemove = ordered.Skip(count).ToList();

        if (entitySet is IRemovableEntitySet<T> removable)
        {
            foreach (var item in toRemove)
            {
                await removable.RemoveAsync(item, cancellationToken);
            }
        }

        return toKeep;

    }
}
