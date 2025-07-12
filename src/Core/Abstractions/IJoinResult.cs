using System;
using System.Linq.Expressions;

namespace Kafka.Ksql.Linq.Core.Abstractions;

public interface IJoinResult<TOuter, TInner>
       where TOuter : class
       where TInner : class
{
    IEntitySet<TResult> Select<TResult>(
        Expression<Func<TOuter, TInner, TResult>> resultSelector) where TResult : class;

    IJoinResult<TOuter, TInner, TThird> Join<TThird, TKey>(
        IEntitySet<TThird> third,
        Expression<Func<TOuter, TKey>> outerKeySelector,
        Expression<Func<TThird, TKey>> thirdKeySelector) where TThird : class;

    IJoinResult<TOuter, TInner, TThird> Join<TThird, TKey>(
        IEntitySet<TThird> third,
        Expression<Func<TInner, TKey>> innerKeySelector,
        Expression<Func<TThird, TKey>> thirdKeySelector) where TThird : class;
}

public interface IJoinResult<TOuter, TInner, TThird>
    where TOuter : class
    where TInner : class
    where TThird : class
{
    IEntitySet<TResult> Select<TResult>(
        Expression<Func<TOuter, TInner, TThird, TResult>> resultSelector) where TResult : class;
}
