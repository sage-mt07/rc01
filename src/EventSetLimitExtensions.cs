using Kafka.Ksql.Linq.Core.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Kafka.Ksql.Linq;

public static class EventSetLimitExtensions
{
    public static async Task<List<T>> Limit<T>(this IEntitySet<T> set, int count, CancellationToken cancellationToken = default) where T : class
    {
        if (set == null) throw new ArgumentNullException(nameof(set));
        if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));

        var list = await set.ToListAsync(cancellationToken);
        var barTimeProp = typeof(T).GetProperty("BarTime", BindingFlags.Public | BindingFlags.Instance);
        if (barTimeProp != null)
        {
            list = list.OrderByDescending(x => (DateTime)barTimeProp.GetValue(x)!).ToList();
        }
        var limited = list.Take(count).ToList();

        foreach (var extra in list.Skip(count))
        {
            await set.RemoveAsync(extra, cancellationToken);
        }

        return limited;
    }
}
