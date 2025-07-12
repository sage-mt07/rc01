using Kafka.Ksql.Linq.Core.Abstractions;
using System;
using System.Linq.Expressions;

namespace Kafka.Ksql.Linq.Query.Linq;

public interface IJoinResult<TOuter, TInner, TThird>
    where TOuter : class
    where TInner : class
    where TThird : class
{
    IEntitySet<TResult> Select<TResult>(Expression<Func<TOuter, TInner, TThird, TResult>> resultSelector) where TResult : class;
}
