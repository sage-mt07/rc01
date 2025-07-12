using Kafka.Ksql.Linq.Configuration;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.StateStore;
using Kafka.Ksql.Linq.StateStore.Management;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

#nullable enable

namespace Kafka.Ksql.Linq.Tests.StateStore;

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

        public Task AddAsync(T entity, CancellationToken cancellationToken = default)
        {
            Added.Add(entity);
            Items.Add(entity);
            return Task.CompletedTask;
        }

        public Task<List<T>> ToListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new List<T>(Items));

        public Task ForEachAsync(Func<T, Task> action, TimeSpan timeout = default, CancellationToken cancellationToken = default)
            => Task.WhenAll(Items.Select(action));

        public string GetTopicName() => _model.TopicName ?? typeof(T).Name.ToLowerInvariant();
        public EntityModel GetEntityModel() => _model;
        public IKsqlContext GetContext() => _context;
        public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            foreach (var item in Items)
            {
                yield return item;
                await Task.Yield();
            }
        }
    }

    private class Sample
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    private static EntityModel CreateModel()
    {
        return new EntityModel
        {
            EntityType = typeof(Sample),
            TopicName = "orders",
            AllProperties = typeof(Sample).GetProperties(),
            KeyProperties = new[] { typeof(Sample).GetProperty(nameof(Sample.Id))! }
        };
    }

    private static WindowedEntitySet<Sample> CreateSet(out StubSet<Sample> baseSet, out StateStoreManager manager)
    {
        var context = new DummyContext();
        var model = CreateModel();
        baseSet = new StubSet<Sample>(context, model);
        manager = new StateStoreManager(Options.Create(new KsqlDslOptions()));
        return new WindowedEntitySet<Sample>(baseSet, 5, manager, model);
    }

    [Fact]
    public async Task AddAsync_PersistsEntityToStoreAndBase()
    {
        var set = CreateSet(out var baseSet, out _);
        var entity = new Sample { Id = 1, Name = "a" };

        await set.AddAsync(entity);

        Assert.Contains(entity, baseSet.Added);
        var stored = set.GetStateStore().All().Select(kv => kv.Value).ToList();
        Assert.Contains(entity, stored);
    }

    [Fact]
    public async Task ToListAsync_MergesStoreAndBaseData()
    {
        var set = CreateSet(out var baseSet, out var manager);
        var store = set.GetStateStore();
        var e1 = new Sample { Id = 1 };
        var e2 = new Sample { Id = 2 };
        baseSet.Items.Add(e1);
        store.Put("2", e2);

        var list = await set.ToListAsync();

        Assert.Equal(2, list.Count);
        Assert.Contains(e1, list);
        Assert.Contains(e2, list);
    }

    [Fact]
    public void GetWindowTableName_UsesBaseTopicName()
    {
        var set = CreateSet(out var baseSet, out _);
        var name = set.GetWindowTableName();
        Assert.Equal("orders_WINDOW_5MIN", name);
    }

    [Fact]
    public void WindowExtension_ReturnsSharedStateStore()
    {
        var context = new DummyContext();
        var model = CreateModel();
        var baseSet = new StubSet<Sample>(context, model);

        var w1 = Kafka.Ksql.Linq.StateStore.WindowExtensions.Window(baseSet, 5);
        var w2 = Kafka.Ksql.Linq.StateStore.WindowExtensions.Window(baseSet, 5);

        var store1 = w1.GetStateStore();
        var store2 = w2.GetStateStore();
        Assert.Same(store1, store2);
    }
}
