using Kafka.Ksql.Linq.Core.Modeling;
using Kafka.Ksql.Linq;
using Kafka.Ksql.Linq.Core.Abstractions;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Kafka.Ksql.Linq.Tests;

public class EventSetKafkaContextTests
{
    private class DummyContext : IKsqlContext
    {
        public IEntitySet<T> Set<T>() where T : class => throw new NotImplementedException();
        public object GetEventSet(Type entityType) => throw new NotImplementedException();
        public Dictionary<Type, EntityModel> GetEntityModels() => new();
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private class HeaderSet : EventSet<TestEntity>
    {
        private readonly List<TestEntity> _items;
        public HeaderSet(List<TestEntity> items, EntityModel model) : base(new DummyContext(), model) => _items = items;
        protected override Task SendEntityAsync(TestEntity entity, Dictionary<string, string>? headers, CancellationToken cancellationToken) => Task.CompletedTask;
        public override async IAsyncEnumerator<TestEntity> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            foreach (var item in _items)
            {
                yield return item;
                await Task.Yield();
            }
        }
        public override Task ForEachAsync(Func<TestEntity, KafkaMessageContext, Task> action, TimeSpan timeout = default, CancellationToken cancellationToken = default)
        {
            return base.ForEachAsync((e, ctx) =>
            {
                if (e.Id == 2)
                    ctx.Headers["is_dummy"] = true;
                return action(e, ctx);
            }, timeout, cancellationToken);
        }
    }

    private static EntityModel CreateModel()
    {
        var builder = new ModelBuilder();
        builder.Entity<TestEntity>()
            .WithTopic("test")
            .HasKey(e => e.Id);
        return builder.GetEntityModel<TestEntity>()!;
    }

    [Fact]
    public async Task ForEachAsync_WithContext_HeaderCanBeRead()
    {
        var items = new List<TestEntity> { new TestEntity { Id = 1 }, new TestEntity { Id = 2 } };
        var set = new HeaderSet(items, CreateModel());
        var processed = new List<int>();

        await set.ForEachAsync((msg, ctx) =>
        {
            if (ctx.Headers.TryGetValue("is_dummy", out var d) && d is bool b && b)
                return Task.CompletedTask;
            processed.Add(msg.Id);
            return Task.CompletedTask;
        });

        Assert.Equal(new[] { 1 }, processed.ToArray());
    }
}
