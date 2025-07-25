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
        return new WindowedEntitySet<Sample>(baseSet, 5);
    }

    private class NoTimestamp
    {
        public int Id { get; set; }
    }

    [Fact]
    public void Constructor_RequiresTimestampProperty()
    {
        var context = new DummyContext();
        var model = new EntityModel
        {
            EntityType = typeof(NoTimestamp),
            TopicName = "orders",
            AllProperties = typeof(NoTimestamp).GetProperties(),
            KeyProperties = new[] { typeof(NoTimestamp).GetProperty(nameof(NoTimestamp.Id))! }
        };
        var baseSet = new StubSet<NoTimestamp>(context, model);

        Assert.Throws<InvalidOperationException>(() => new WindowedEntitySet<NoTimestamp>(baseSet, 5));
    }

    [Fact]
    public void GetWindowTableName_UsesBaseTopic()
    {
        var set = CreateSet(out _);
        Assert.Equal("orders_WINDOW_5MIN", set.GetWindowTableName());
    }

    [Fact]
    public async Task AddAsync_DelegatesToBase()
    {
        var set = CreateSet(out var baseSet);
        var sample = new Sample { Id = 1, Timestamp = DateTime.UtcNow };
        await set.AddAsync(sample);
        Assert.Contains(sample, baseSet.Items);
    }
}
