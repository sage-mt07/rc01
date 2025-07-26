using Kafka.Ksql.Linq.Core.Abstractions;
using System;
using System.Linq.Expressions;

namespace Kafka.Ksql.Linq.Query;

internal class ThreeWayJoinResult<TOuter, TInner, TThird> : IJoinResult<TOuter, TInner, TThird>
    where TOuter : class
    where TInner : class
    where TThird : class
{
    private readonly IEntitySet<TOuter> _outer;
    private readonly IEntitySet<TInner> _inner;
    private readonly IEntitySet<TThird> _third;
    private readonly Expression _outerKeySelector;
    private readonly Expression _innerKeySelector;
    private readonly Expression _firstThirdKeySelector;
    private readonly Expression _secondThirdKeySelector;

    public ThreeWayJoinResult(
        IEntitySet<TOuter> outer,
        IEntitySet<TInner> inner,
        IEntitySet<TThird> third,
        Expression outerKeySelector,
        Expression innerKeySelector,
        Expression firstThirdKeySelector,
        Expression secondThirdKeySelector)
    {
        _outer = outer ?? throw new ArgumentNullException(nameof(outer));
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _third = third ?? throw new ArgumentNullException(nameof(third));
        _outerKeySelector = outerKeySelector ?? throw new ArgumentNullException(nameof(outerKeySelector));
        _innerKeySelector = innerKeySelector ?? throw new ArgumentNullException(nameof(innerKeySelector));
        _firstThirdKeySelector = firstThirdKeySelector ?? throw new ArgumentNullException(nameof(firstThirdKeySelector));
        _secondThirdKeySelector = secondThirdKeySelector ?? throw new ArgumentNullException(nameof(secondThirdKeySelector));
    }

    public IEntitySet<TResult> Select<TResult>(
        Expression<Func<TOuter, TInner, TThird, TResult>> resultSelector) where TResult : class
    {
        if (resultSelector == null)
            throw new ArgumentNullException(nameof(resultSelector));

        return new TypedThreeWayJoinResultEntitySet<TOuter, TInner, TThird, TResult>(
                      _outer.GetContext(),
                      CreateResultEntityModel<TResult>(),
                      _outer,
                      _inner,
                      _third,
                      _outerKeySelector,
                      _innerKeySelector,
                      _firstThirdKeySelector,
                      _secondThirdKeySelector,
                      resultSelector);
    }

    private static EntityModel CreateResultEntityModel<TResult>() where TResult : class
    {
        return new EntityModel
        {
            EntityType = typeof(TResult),
            TopicName = $"{typeof(TResult).Name.ToLowerInvariant()}_threewayjoinresult",
            AllProperties = typeof(TResult).GetProperties(),
            KeyProperties = Array.Empty<System.Reflection.PropertyInfo>(),
            ValidationResult = new ValidationResult { IsValid = true }
        };
    }
}
