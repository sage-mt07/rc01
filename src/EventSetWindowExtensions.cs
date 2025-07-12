using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Kafka.Ksql.Linq;

public static class EventSetWindowExtensions
{
    private static readonly MethodInfo WindowMethod = typeof(EventSetWindowExtensions)
        .GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
        .First(m => m.Name == nameof(Window) && m.GetParameters().Length == 2 && m.GetParameters()[1].ParameterType == typeof(WindowDef));

    private static readonly MethodInfo WindowDurationMethod = typeof(EventSetWindowExtensions)
        .GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
        .First(m => m.Name == nameof(Window) && m.GetParameters().Length == 2 && m.GetParameters()[1].ParameterType == typeof(TimeSpan));

    public static IQueryable<T> Window<T>(this IQueryable<T> source, WindowDef windowDef) where T : class
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (windowDef is null) throw new ArgumentNullException(nameof(windowDef));

        var call = Expression.Call(
            null,
            WindowMethod.MakeGenericMethod(typeof(T)),
            source.Expression,
            Expression.Constant(windowDef, typeof(WindowDef)));

        return source.Provider.CreateQuery<T>(call);
    }

    public static IQueryable<T> Window<T>(this IQueryable<T> source, TimeSpan duration) where T : class
    {
        if (source is null) throw new ArgumentNullException(nameof(source));

        var call = Expression.Call(
            null,
            WindowDurationMethod.MakeGenericMethod(typeof(T)),
            source.Expression,
            Expression.Constant(duration, typeof(TimeSpan)));

        return source.Provider.CreateQuery<T>(call);
    }

    public static IQueryable<T> Window<T>(this IQueryable<T> source, int minutes) where T : class
    {
        return Window(source, TimeSpan.FromMinutes(minutes));
    }
}
