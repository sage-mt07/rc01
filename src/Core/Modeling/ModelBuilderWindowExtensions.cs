using System;
using Kafka.Ksql.Linq.Core.Abstractions;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Kafka.Ksql.Linq.Core.Modeling;

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
        return new WindowSelectionBuilder<TEntity, TSchedule>(modelBuilder);
    }
}

public class WindowSelectionBuilder<TEntity, TSchedule>
{
    private readonly IModelBuilder _modelBuilder;

    internal WindowSelectionBuilder(IModelBuilder modelBuilder)
    {
        _modelBuilder = modelBuilder;
    }

    public void Select<TResult>(Expression<Func<WindowGrouping<TEntity>, TResult>> selector) where TResult : class
    {
        var builder = (EntityModelBuilder<TResult>)_modelBuilder.Entity<TResult>();
        var model = builder.GetModel();
        model.BarTimeSelector = ExtractBarTimeSelector(selector);
        // placeholder for DSL registration of window aggregation
    }

    private static LambdaExpression? ExtractBarTimeSelector<TResult>(Expression<Func<WindowGrouping<TEntity>, TResult>> selector)
    {
        if (selector.Body is MemberInitExpression init)
        {
            foreach (var binding in init.Bindings.OfType<MemberAssignment>())
            {
                if (binding.Expression is MemberExpression member && member.Member.Name == nameof(WindowGrouping<object>.BarStart))
                {
                    var param = Expression.Parameter(typeof(TResult), "x");
                    var prop = Expression.Property(param, (PropertyInfo)binding.Member);
                    return Expression.Lambda(prop, param);
                }
            }
        }
        return null;
    }
}

public class WindowGrouping<T>
{
    public object Key { get; set; } = default!;
    public int BarWidth { get; set; }
    public DateTime BarStart { get; set; }
    public IQueryable<T> Source { get; set; } = default!;
}
