using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Core.Window;
using System;

namespace Kafka.Ksql.Linq.Core.Extensions;

public static class WindowFilterExtensions
{
    /// <summary>
    /// Filter an entity set by its WindowMinutes property.
    /// </summary>
    public static IEntitySet<T> Window<T>(this IEntitySet<T> entitySet, int windowMinutes) where T : class
    {
        if (entitySet == null) throw new ArgumentNullException(nameof(entitySet));
        return new WindowFilteredEntitySet<T>(entitySet, windowMinutes);
    }
}
