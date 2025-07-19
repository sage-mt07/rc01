using DailyComparisonLib;
using DailyComparisonLib.Models;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Core.Modeling;
using Kafka.Ksql.Linq.Core.Context;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace DailyComparisonLib.Tests;

public class AggregatorTests
{
    private class DummySet<T> : IQueryable<T>, IEntitySet<T> where T : class
    {
        private readonly List<T> _items;
        private IQueryable<T> _query;
        private readonly IKsqlContext _context;
        private readonly EntityModel _model;
        public DummySet(IKsqlContext context)
        {
            _items = new List<T>();
            _query = _items.AsQueryable();
            _context = context;
            _model = new EntityModel { EntityType = typeof(T), TopicName = typeof(T).Name.ToLowerInvariant(), AllProperties = typeof(T).GetProperties(), KeyProperties = Array.Empty<System.Reflection.PropertyInfo>() };
        }
        public Type ElementType => _query.ElementType;
        public Expression Expression => _query.Expression;
        public IQueryProvider Provider => _query.Provider;
        public Task AddAsync(T entity, CancellationToken cancellationToken = default) { _items.Add(entity); _query = _items.AsQueryable(); return Task.CompletedTask; }
        public void AddItem(T item) { _items.Add(item); _query = _items.AsQueryable(); }
        public Task<List<T>> ToListAsync(CancellationToken cancellationToken = default) => Task.FromResult(_items.ToList());
        public Task ForEachAsync(Func<T, Task> action, TimeSpan timeout = default, CancellationToken cancellationToken = default) => Task.WhenAll(_items.Select(action));
        public string GetTopicName() => _model.TopicName ?? typeof(T).Name.ToLowerInvariant();
        public EntityModel GetEntityModel() => _model;
        public IKsqlContext GetContext() => _context;
        public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            foreach (var i in _items)
            {
                yield return i;
                await Task.Yield();
            }
        }
        public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _items.GetEnumerator();
    }

    private class DummyContext : IKsqlContext
    {
        private readonly Dictionary<Type, object> _sets = new();
        public void AddSet<T>(DummySet<T> set) where T : class => _sets[typeof(T)] = set;
        public IEntitySet<T> Set<T>() where T : class => (IEntitySet<T>)_sets[typeof(T)];
        public object GetEventSet(Type entityType) => _sets[entityType];
        public Dictionary<Type, EntityModel> GetEntityModels() => new();
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Aggregate_ComputesDailyComparison()
    {
        var context = new DummyContext();
        var rateSet = new DummySet<Rate>(context);
        var scheduleSet = new DummySet<MarketSchedule>(context);
        var dailySet = new DummySet<DailyComparison>(context);
        var candleSet = new DummySet<RateCandle>(context);
        context.AddSet(rateSet);
        context.AddSet(scheduleSet);
        context.AddSet(dailySet);
        context.AddSet(candleSet);

        rateSet.AddItem(new Rate { Broker = "b", Symbol = "s", RateId = 1, RateTimestamp = new DateTime(2024,1,1,1,0,0), Bid = 1m, Ask = 1.1m });
        rateSet.AddItem(new Rate { Broker = "b", Symbol = "s", RateId = 2, RateTimestamp = new DateTime(2024,1,1,2,0,0), Bid = 2m, Ask = 2.1m });
        scheduleSet.AddItem(new MarketSchedule { Broker="b", Symbol="s", Date = new DateTime(2024,1,1), OpenTime = new DateTime(2024,1,1,0,0,0), CloseTime = new DateTime(2024,1,1,23,59,59)});

        var aggregator = new Aggregator(new KafkaKsqlContextStub(context));
        await aggregator.AggregateAsync(new DateTime(2024,1,1));

        var result = Assert.Single(dailySet);
        Assert.Equal(2.1m, result.High);
        Assert.Equal(1m, result.Low);
        Assert.Equal(2.1m, result.Close);
        Assert.Equal(0m, result.Diff);

        Assert.Equal(6, candleSet.ToListAsync().Result.Count);
    }

    private class KafkaKsqlContextStub : KafkaKsqlContext
    {
        private readonly IKsqlContext _inner;
        public KafkaKsqlContextStub(IKsqlContext inner) : base(new KafkaContextOptions()) { _inner = inner; }
        protected override bool SkipSchemaRegistration => true;
        protected override IEntitySet<T> CreateEntitySet<T>(EntityModel entityModel) where T : class
        {
            return (IEntitySet<T>)_inner.GetEventSet(typeof(T));
        }
    }
}
