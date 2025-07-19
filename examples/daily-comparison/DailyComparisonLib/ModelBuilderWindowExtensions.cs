using System;
using Kafka.Ksql.Linq.Core.Abstractions;
using System.Linq;
using System.Linq.Expressions;

namespace Kafka.Ksql.Linq;

public static class WindowDslExtensions
{
    public static IQueryable<T> Window<T>(this IQueryable<T> source, params int[] windowSizes)
    {
        return source;
    }
}

public static class WindowGroupingExtensions
{
    public static WindowInfo Window<T>(this IGrouping<object, T> grouping)
    {
        return new WindowInfo();
    }
}

public class WindowInfo
{
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public TimeSpan Size { get; set; }
}

public static class ModelBuilderWindowExtensions
{
    public static void WithWindow<T>(this IModelBuilder modelBuilder, int[] windows)
        where T : class
    {
        // placeholder for DSL registration
    }

    public static WindowSelectionBuilder<TEntity, TSchedule> WithWindow<TEntity, TSchedule>(
        this IModelBuilder modelBuilder,
        int[] windows,
        Expression<Func<TEntity, DateTime>> timeSelector,
        Expression<Func<TEntity, object>> rateToScheduleKey,
        Expression<Func<TSchedule, object>> scheduleKey)
        where TEntity : class
        where TSchedule : class
    {
        // placeholder for DSL registration
        return new WindowSelectionBuilder<TEntity, TSchedule>();
    }
}

public class WindowSelectionBuilder<TEntity, TSchedule>
{
    public void Select<TResult>(Func<WindowGrouping<TEntity>, TResult> selector)
    {
        // placeholder for DSL registration
    }
}

public class WindowGrouping<T>
{
    public object Key { get; set; } = default!;
    public int BarWidth { get; set; }
    public DateTime BarStart { get; set; }
    public IQueryable<T> Source { get; set; } = default!;
}
