using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Core.Window;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

#nullable enable

namespace Kafka.Ksql.Linq.Tests.Core.Window;

public class WindowCollectionTests
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
        public readonly List<T> Items = new();
        public StubSet(IKsqlContext context, EntityModel model)
        {
            _context = context;
            _model = model;
        }
        public Task AddAsync(T entity, Dictionary<string, string>? headers = null, CancellationToken cancellationToken = default) { Items.Add(entity); return Task.CompletedTask; }
        public Task RemoveAsync(T entity, CancellationToken cancellationToken = default) { Items.Remove(entity); return Task.CompletedTask; }
        public Task<List<T>> ToListAsync(CancellationToken cancellationToken = default) => Task.FromResult(new List<T>(Items));
        public Task ForEachAsync(Func<T, Task> action, TimeSpan timeout = default, CancellationToken cancellationToken = default) => Task.WhenAll(Items.Select(action));
        public Task ForEachAsync(Func<T, KafkaMessageContext, Task> action, TimeSpan timeout = default, CancellationToken cancellationToken = default) => Task.WhenAll(Items.Select(i => action(i, new KafkaMessageContext())));
        public string GetTopicName() => _model.TopicName ?? typeof(T).Name.ToLowerInvariant();
        public EntityModel GetEntityModel() => _model;
        public IKsqlContext GetContext() => _context;
        public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) { foreach (var i in Items) { yield return i; await Task.Yield(); } }
    }

    private class Sample
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
    }

    private static WindowCollection<Sample> CreateCollection(out StubSet<Sample> baseSet)
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
        return new WindowCollection<Sample>(baseSet, new[] { 5, 10 });
    }

    [Fact]
    public void Indexer_NonConfiguredSize_Throws()
    {
        var collection = CreateCollection(out _);
        Assert.Throws<ArgumentException>(() => collection[20]);
    }

    [Fact]
    public void Indexer_SameSize_ReturnsSameInstance()
    {
        var collection = CreateCollection(out _);
        var w1 = collection[5];
        var w2 = collection[5];
        Assert.Same(w1, w2);
    }

    [Fact]
    public async Task GetAllWindowsAsync_ReturnsDataForAllSizes()
    {
        var collection = CreateCollection(out var baseSet);
        baseSet.Items.Add(new Sample { Id = 1, Timestamp = DateTime.UtcNow });

        var result = await collection.GetAllWindowsAsync();

        Assert.Equal(new[] {5,10}, result.Keys.OrderBy(x=>x).ToArray());
    }
}
