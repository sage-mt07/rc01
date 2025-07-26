using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Core.Window;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

#nullable enable

namespace Kafka.Ksql.Linq.Tests.Core.Window;

public class WindowAggregatedEntitySetTests
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
        public StubSet(IKsqlContext context, EntityModel model)
        {
            _context = context;
            _model = model;
        }
        public Task AddAsync(T entity, Dictionary<string, string>? headers = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveAsync(T entity, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<List<T>> ToListAsync(CancellationToken cancellationToken = default) => Task.FromResult(new List<T>());
        public Task ForEachAsync(Func<T, Task> action, TimeSpan timeout = default, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ForEachAsync(Func<T, KafkaMessage<T, object>, Task> action, TimeSpan timeout = default, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public string GetTopicName() => _model.TopicName ?? typeof(T).Name.ToLowerInvariant();
        public EntityModel GetEntityModel() => _model;
        public IKsqlContext GetContext() => _context;
        public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) { await Task.CompletedTask; yield break; }
    }

    private class SourceEntity
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
    }

    private class ResultEntity
    {
        public int Count { get; set; }
    }

    private static WindowAggregatedEntitySet<SourceEntity, int, ResultEntity> CreateSet()
    {
        var context = new DummyContext();
        var model = new EntityModel
        {
            EntityType = typeof(SourceEntity),
            TopicName = "orders",
            AllProperties = typeof(SourceEntity).GetProperties(),
            KeyProperties = new[] { typeof(SourceEntity).GetProperty(nameof(SourceEntity.Id))! }
        };
        var baseSet = new StubSet<SourceEntity>(context, model);
        Expression<Func<SourceEntity, int>> group = x => x.Id;
        Expression<Func<IGrouping<int, SourceEntity>, ResultEntity>> agg = g => new ResultEntity { Count = g.Count() };
        var config = new WindowAggregationConfig { WindowSize = TimeSpan.FromMinutes(5) };
        return new WindowAggregatedEntitySet<SourceEntity, int, ResultEntity>(baseSet, 5, group, agg, config);
    }

    [Fact]
    public void GetTopicName_BeginsWithBaseTopic()
    {
        var set = CreateSet();
        Assert.StartsWith("orders_WINDOW_5MIN_AGG_", set.GetTopicName());
    }

    [Fact]
    public async Task AddAsync_ThrowsNotSupported()
    {
        var set = CreateSet();
        await Assert.ThrowsAsync<NotSupportedException>(() => set.AddAsync(new ResultEntity()));
    }

    [Fact]
    public void ToString_IncludesTypeAndWindow()
    {
        var set = CreateSet();
        var text = set.ToString();
        Assert.Contains("ResultEntity", text);
        Assert.Contains("5min", text);
    }
}
