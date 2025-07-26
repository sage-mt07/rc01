using Kafka.Ksql.Linq.Core.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace Kafka.Ksql.Linq.Query;

internal class TypedThreeWayJoinResultEntitySet<TOuter, TInner, TThird, TResult> : IEntitySet<TResult>
    where TOuter : class
    where TInner : class
    where TThird : class
    where TResult : class
{
    private readonly IKsqlContext _context;
    private readonly EntityModel _entityModel;
    private readonly IEntitySet<TOuter> _outerEntitySet;
    private readonly IEntitySet<TInner> _innerEntitySet;
    private readonly IEntitySet<TThird> _thirdEntitySet;
    private readonly Expression _outerKeySelector;
    private readonly Expression _innerKeySelector;
    private readonly Expression _firstThirdKeySelector;
    private readonly Expression _secondThirdKeySelector;
    private readonly Expression<Func<TOuter, TInner, TThird, TResult>> _resultSelector;

    public TypedThreeWayJoinResultEntitySet(
        IKsqlContext context,
        EntityModel entityModel,
        IEntitySet<TOuter> outerEntitySet,
        IEntitySet<TInner> innerEntitySet,
        IEntitySet<TThird> thirdEntitySet,
        Expression outerKeySelector,
        Expression innerKeySelector,
        Expression firstThirdKeySelector,
        Expression secondThirdKeySelector,
        Expression<Func<TOuter, TInner, TThird, TResult>> resultSelector)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _entityModel = entityModel ?? throw new ArgumentNullException(nameof(entityModel));
        _outerEntitySet = outerEntitySet ?? throw new ArgumentNullException(nameof(outerEntitySet));
        _innerEntitySet = innerEntitySet ?? throw new ArgumentNullException(nameof(innerEntitySet));
        _thirdEntitySet = thirdEntitySet ?? throw new ArgumentNullException(nameof(thirdEntitySet));
        _outerKeySelector = outerKeySelector ?? throw new ArgumentNullException(nameof(outerKeySelector));
        _innerKeySelector = innerKeySelector ?? throw new ArgumentNullException(nameof(innerKeySelector));
        _firstThirdKeySelector = firstThirdKeySelector ?? throw new ArgumentNullException(nameof(firstThirdKeySelector));
        _secondThirdKeySelector = secondThirdKeySelector ?? throw new ArgumentNullException(nameof(secondThirdKeySelector));
        _resultSelector = resultSelector ?? throw new ArgumentNullException(nameof(resultSelector));
    }

    public async Task<List<TResult>> ToListAsync(CancellationToken cancellationToken = default)
    {
        await Task.Delay(100, cancellationToken);
        return new List<TResult>();
    }

    public Task AddAsync(TResult entity, Dictionary<string,string>? headers = null, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Cannot add entities to a three-way join result set");
    }

    public Task RemoveAsync(TResult entity, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Cannot remove entities from a three-way join result set");
    }

    public Task ForEachAsync(Func<TResult, Task> action, TimeSpan timeout = default, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("ForEachAsync not supported on three-way join result sets");
    }

    public Task ForEachAsync(Func<TResult, KafkaMessage<TResult,object>, Task> action, TimeSpan timeout = default, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("ForEachAsync not supported on three-way join result sets");
    }

    public string GetTopicName() => (_entityModel.TopicName ?? typeof(TResult).Name).ToLowerInvariant();
    public EntityModel GetEntityModel() => _entityModel;
    public IKsqlContext GetContext() => _context;

    public async IAsyncEnumerator<TResult> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        var results = await ToListAsync(cancellationToken);
        foreach (var item in results)
        {
            yield return item;
        }
    }
}
