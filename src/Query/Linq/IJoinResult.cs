using Kafka.Ksql.Linq.Core.Abstractions;
using System;
using System.Linq.Expressions;

namespace Kafka.Ksql.Linq.Query.Linq;

public interface IJoinResult<TOuter, TInner>
    where TOuter : class
    where TInner : class
{
    IEntitySet<TResult> Select<TResult>(Expression<Func<TOuter, TInner, TResult>> resultSelector) where TResult : class;
    IJoinResult<TOuter, TInner, TThird> Join<TThird, TKey>(IEntitySet<TThird> third, Expression<Func<TOuter, TKey>> outerKeySelector, Expression<Func<TThird, TKey>> thirdKeySelector) where TThird : class;
    IJoinResult<TOuter, TInner, TThird> Join<TThird, TKey>(IEntitySet<TThird> third, Expression<Func<TInner, TKey>> innerKeySelector, Expression<Func<TThird, TKey>> thirdKeySelector) where TThird : class;
}

