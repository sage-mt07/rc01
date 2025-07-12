using Kafka.Ksql.Linq.Core.Abstractions;
using System;
using System.Linq.Expressions;

namespace Kafka.Ksql.Linq.Query.Linq;

public interface IJoinableEntitySet<T> where T : class
{
    IJoinResult<T, TInner> Join<TInner, TKey>(
        IEntitySet<TInner> inner,
        Expression<Func<T, TKey>> outerKeySelector,
        Expression<Func<TInner, TKey>> innerKeySelector) where TInner : class;
}
