using System;
using System.Linq;

namespace Kafka.Ksql.Linq
{
    /// <summary>
    /// Provides access to WINDOWSTART and WINDOWEND metadata within LINQ expressions.
    /// These methods are intended only for expression translation and should not
    /// be executed at runtime.
    /// </summary>
    public static class WindowInfoExtensions
    {
        public static DateTime WindowStart<TSource, TKey>(this IGrouping<TKey, TSource> source)
        {
            throw new NotSupportedException("WindowStart is for expression translation only.");
        }

        public static DateTime WindowEnd<TSource, TKey>(this IGrouping<TKey, TSource> source)
        {
            throw new NotSupportedException("WindowEnd is for expression translation only.");
        }
    }
}
