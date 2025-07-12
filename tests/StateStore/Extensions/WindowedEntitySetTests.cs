using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.StateStore.Core;
using Kafka.Ksql.Linq.Configuration;
using Kafka.Ksql.Linq.StateStore.Extensions;
using Kafka.Ksql.Linq.StateStore.Management;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.StateStore.Extensions;

public class WindowedEntitySetTests
{
    private class DummyContext : IKsqlContext
    {
        public IEntitySet<T> Set<T>() where T : class => throw new NotImplementedException();
        public object GetEventSet(Type entityType) => throw new NotImplementedException();
        public Dictionary<Type, EntityModel> GetEntityModels() => new();
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private class StubSet<T> : IEntitySet<T> where T : class
    {
        private readonly IKsqlContext _context;
        private readonly EntityModel _model;
        public readonly List<T> Added = new();
        public readonly List<T> Items = new();
        public StubSet(IKsqlContext context, EntityModel model)
        {
            _context = context;
            _model = model;
        }
        public Task AddAsync(T entity, CancellationToken cancellationToken = default) { Added.Add(entity); Items.Add(entity); return Task.CompletedTask; }
        public Task<List<T>> ToListAsync(CancellationToken cancellationToken = default) => Task.FromResult(new List<T>(Items));
        public Task ForEachAsync(Func<T, Task> action, TimeSpan timeout = default, CancellationToken cancellationToken = default) => Task.WhenAll(Items.Select(action));
        public string GetTopicName() => _model.TopicName ?? typeof(T).Name.ToLowerInvariant();
        public EntityModel GetEntityModel() => _model;
        public IKsqlContext GetContext() => _context;
        public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) { foreach (var i in Items) { yield return i; await Task.Yield(); } }
    }

    private class Sample
    {
        public int Id { get; set; }
    }

    private static WindowedEntitySet<Sample> CreateSet(out StubSet<Sample> baseSet)
    {
        var context = new DummyContext();
        var model = new EntityModel
        {
            EntityType = typeof(Sample),
            TopicName = "orders",
            AllProperties = typeof(Sample).GetProperties(),
            KeyProperties = new[] { typeof(Sample).GetProperty(nameof(Sample.Id))! }
        };
        baseSet = new StubSet<Sample>(context, model);
        var manager = new StateStoreManager(Options.Create(new KsqlDslOptions()));
        return new WindowedEntitySet<Sample>(baseSet, 5, manager, model);
    }

    [Fact]
    public async Task AddAsync_PersistsEntity()
    {
        var set = CreateSet(out var baseSet);
        var entity = new Sample { Id = 1 };
        await set.AddAsync(entity);
        Assert.Contains(entity, baseSet.Added);
        Assert.Contains(entity, set.GetStateStore().All().Select(k=>k.Value));
    }

    [Fact]
    public void GetWindowTableName_UsesBaseTopic()
    {
        var set = CreateSet(out _);
        Assert.Equal("orders_WINDOW_5MIN", set.GetWindowTableName());
    }
}
