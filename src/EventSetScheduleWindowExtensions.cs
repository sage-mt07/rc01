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

    private static readonly MethodInfo BasedOnMethod = typeof(ScheduleWindowBuilder<T>)
        .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
        .Single(m => m.Name == nameof(BasedOnImpl));

    public IQueryable<T> BasedOn<TSchedule>(Expression<Func<T, object>> keySelector) where TSchedule : class
    {
        if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));
        var call = Expression.Call(null,
            BasedOnMethod.MakeGenericMethod(typeof(T), typeof(TSchedule)),
            _source.Expression,
            keySelector,
            Expression.Constant(null, typeof(string)),
            Expression.Constant(null, typeof(string)));
        return _source.Provider.CreateQuery<T>(call);
    }

    public IQueryable<T> BasedOn<TSchedule>(Expression<Func<T, object>> keySelector, string openPropertyName, string closePropertyName) where TSchedule : class
    {
        if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));
        if (string.IsNullOrWhiteSpace(openPropertyName) || string.IsNullOrWhiteSpace(closePropertyName))
            throw new ArgumentException("Property names cannot be null or empty");

        var call = Expression.Call(null,
            BasedOnMethod.MakeGenericMethod(typeof(T), typeof(TSchedule)),
            _source.Expression,
            keySelector,
            Expression.Constant(openPropertyName, typeof(string)),
            Expression.Constant(closePropertyName, typeof(string)));
        return _source.Provider.CreateQuery<T>(call);
    }

    private static IQueryable<TSource> BasedOnImpl<TSource, TSchedule>(IQueryable<TSource> source, Expression<Func<TSource, object>> keySelector, string? openPropertyName, string? closePropertyName)
        where TSource : class
        where TSchedule : class
    {
        if (source is not Core.Abstractions.IEntitySet<TSource> entitySet)
            throw new NotSupportedException("BasedOn can only be used on IEntitySet sources.");

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

        Core.Attributes.ScheduleRangeAttribute? rangeAttr = null;
        PropertyInfo? rangeProp = null;

        if (!string.IsNullOrWhiteSpace(openPropertyName) && !string.IsNullOrWhiteSpace(closePropertyName))
        {
            rangeAttr = new Core.Attributes.ScheduleRangeAttribute(openPropertyName, closePropertyName);
        }
        else
        {
            rangeAttr = typeof(TSchedule).GetCustomAttribute<Core.Attributes.ScheduleRangeAttribute>();
            if (rangeAttr == null)
            {
                rangeProp = typeof(TSchedule).GetProperties()
                    .FirstOrDefault(p => p.GetCustomAttribute<Core.Attributes.ScheduleRangeAttribute>() != null)
                    ?? throw new InvalidOperationException($"{typeof(TSchedule).Name} requires [ScheduleRange] attribute");
                rangeAttr = rangeProp.GetCustomAttribute<Core.Attributes.ScheduleRangeAttribute>();
            }
        }

        PropertyInfo openProp;
        PropertyInfo closeProp;
        Expression openExpr;
        Expression closeExpr;

        if (rangeProp == null)
        {
            openProp = typeof(TSchedule).GetProperty(rangeAttr!.OpenPropertyName)
                ?? throw new InvalidOperationException($"Schedule type missing open property '{rangeAttr.OpenPropertyName}'");
            closeProp = typeof(TSchedule).GetProperty(rangeAttr.ClosePropertyName)
                ?? throw new InvalidOperationException($"Schedule type missing close property '{rangeAttr.ClosePropertyName}'");
            openExpr = Expression.Property(schParam, openProp);
            closeExpr = Expression.Property(schParam, closeProp);
        }
        else
        {
            openProp = rangeProp.PropertyType.GetProperty(rangeAttr!.OpenPropertyName)
                ?? throw new InvalidOperationException($"Schedule range type {rangeProp.PropertyType.Name} missing open property '{rangeAttr.OpenPropertyName}'");
            closeProp = rangeProp.PropertyType.GetProperty(rangeAttr.ClosePropertyName)
                ?? throw new InvalidOperationException($"Schedule range type {rangeProp.PropertyType.Name} missing close property '{rangeAttr.ClosePropertyName}'");
            var rangeExpr = Expression.Property(schParam, rangeProp);
            openExpr = Expression.Property(rangeExpr, openProp);
            closeExpr = Expression.Property(rangeExpr, closeProp);
        }

        var timestampProp = typeof(TSource).GetProperties()
            .FirstOrDefault(p => p.PropertyType == typeof(DateTime) || p.PropertyType == typeof(DateTimeOffset))
            ?? throw new InvalidOperationException($"{typeof(TSource).Name} requires a DateTime property for event time");

        var eventTime = Expression.Property(srcParam, timestampProp);
        var openTime = openExpr;
        var closeTime = closeExpr;

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
