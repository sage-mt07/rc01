using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Kafka.Ksql.Linq;

public static class EventSetScheduleWindowExtensions
{
    public static ScheduleWindowBuilder<T> Window<T>(this IQueryable<T> source) where T : class
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        return new ScheduleWindowBuilder<T>(source);
    }
}

public class ScheduleWindowBuilder<T> where T : class
{
    private readonly IQueryable<T> _source;
    internal ScheduleWindowBuilder(IQueryable<T> source)
    {
        _source = source;
    }

    private static readonly MethodInfo BaseOnMethod = typeof(ScheduleWindowBuilder<T>)
        .GetMethod(nameof(BaseOnImpl), BindingFlags.NonPublic | BindingFlags.Static)!;

    public IQueryable<T> BaseOn<TSchedule>(Expression<Func<T, object>> keySelector) where TSchedule : class
    {
        if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));
        var call = Expression.Call(null,
            BaseOnMethod.MakeGenericMethod(typeof(T), typeof(TSchedule)),
            _source.Expression,
            keySelector);
        return _source.Provider.CreateQuery<T>(call);
    }

    private static IQueryable<TSource> BaseOnImpl<TSource, TSchedule>(IQueryable<TSource> source, Expression<Func<TSource, object>> keySelector)
        where TSource : class
        where TSchedule : class
    {
        if (source is not Core.Abstractions.IEntitySet<TSource> entitySet)
            throw new NotSupportedException("BaseOn can only be used on IEntitySet sources.");

        var context = entitySet.GetContext();
        var scheduleSetObj = context.GetEventSet(typeof(TSchedule));
        if (scheduleSetObj is not IQueryable<TSchedule> scheduleSet)
            throw new InvalidOperationException($"Schedule set for {typeof(TSchedule).Name} is not queryable");

        var srcParam = Expression.Parameter(typeof(TSource), "e");
        var schParam = Expression.Parameter(typeof(TSchedule), "s");

        var srcKey = Expression.Invoke(keySelector, srcParam);

        var keyPropName = ((keySelector.Body as MemberExpression)?.Member as PropertyInfo)?.Name
            ?? (((keySelector.Body as UnaryExpression)?.Operand as MemberExpression)?.Member as PropertyInfo)?.Name
            ?? throw new InvalidOperationException("keySelector must select a property");

        var schKeyProp = typeof(TSchedule).GetProperty(keyPropName)
            ?? throw new InvalidOperationException($"Schedule type missing key property '{keyPropName}'");

        var schKey = Expression.Property(schParam, schKeyProp);

        var equal = Expression.Equal(
            Expression.Convert(schKey, typeof(object)),
            Expression.Convert(srcKey, typeof(object)));

        var openProp = typeof(TSchedule).GetProperties()
            .FirstOrDefault(p => p.GetCustomAttribute<Core.Attributes.ScheduleOpenAttribute>() != null)
            ?? throw new InvalidOperationException($"{typeof(TSchedule).Name} requires [ScheduleOpen] property");
        var closeProp = typeof(TSchedule).GetProperties()
            .FirstOrDefault(p => p.GetCustomAttribute<Core.Attributes.ScheduleCloseAttribute>() != null)
            ?? throw new InvalidOperationException($"{typeof(TSchedule).Name} requires [ScheduleClose] property");

        var timestampProp = typeof(TSource).GetProperties()
            .FirstOrDefault(p => p.PropertyType == typeof(DateTime) || p.PropertyType == typeof(DateTimeOffset))
            ?? throw new InvalidOperationException($"{typeof(TSource).Name} requires a DateTime property for event time");

        var eventTime = Expression.Property(srcParam, timestampProp);
        var openTime = Expression.Property(schParam, openProp);
        var closeTime = Expression.Property(schParam, closeProp);

        var openCond = Expression.GreaterThanOrEqual(
            Expression.Convert(eventTime, typeof(DateTime)),
            Expression.Convert(openTime, typeof(DateTime)));
        var closeCond = Expression.LessThan(
            Expression.Convert(eventTime, typeof(DateTime)),
            Expression.Convert(closeTime, typeof(DateTime)));

        var timeRange = Expression.AndAlso(openCond, closeCond);
        var schedulePredicate = Expression.AndAlso(equal, timeRange);

        var scheduleLambda = Expression.Lambda<Func<TSchedule, bool>>(schedulePredicate, schParam);

        var anyCall = Expression.Call(
            typeof(Queryable),
            nameof(Queryable.Any),
            new[] { typeof(TSchedule) },
            scheduleSet.Expression,
            scheduleLambda);

        var whereLambda = Expression.Lambda<Func<TSource, bool>>(anyCall, srcParam);

        var whereCall = Expression.Call(
            typeof(Queryable),
            nameof(Queryable.Where),
            new[] { typeof(TSource) },
            source.Expression,
            whereLambda);

        return source.Provider.CreateQuery<TSource>(whereCall);
    }
}
