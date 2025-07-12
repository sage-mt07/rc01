using Kafka.Ksql.Linq.Core.Modeling;
using Kafka.Ksql.Linq.Configuration;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.StateStore.Extensions;
using Kafka.Ksql.Linq.StateStore.Management;
using Kafka.Ksql.Linq.StateStore;
using Xunit;
using System.Collections.Generic;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Kafka.Ksql.Linq.Tests.Window.Finalization;

public class UseFinalizedDslTests
{
    private class DummyContext : IKsqlContext
    {
        private class StubSet<T> : IEntitySet<T> where T : class
        {
            private readonly IKsqlContext _context;

            public StubSet(IKsqlContext context)
            {
                _context = context;
            }

            public Task AddAsync(T entity, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task<List<T>> ToListAsync(CancellationToken cancellationToken = default) => Task.FromResult(new List<T>());
            public Task ForEachAsync(Func<T, Task> action, TimeSpan timeout = default, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public string GetTopicName() => typeof(T).Name;
            public EntityModel GetEntityModel()
            {
                var builder = new ModelBuilder();
                builder.Entity<T>()
                    .WithTopic("orders");
                return builder.GetEntityModel<T>()!;
            }
            public IKsqlContext GetContext() => _context;
            public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) { await Task.CompletedTask; yield break; }
        }

        public IEntitySet<T> Set<T>() where T : class => new StubSet<T>(this);
        public object GetEventSet(Type entityType) => throw new NotImplementedException();
        public Dictionary<Type, EntityModel> GetEntityModels() => new();
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private class Sample { public int Id { get; set; } }

    [Fact]
    public void UseFinalized_ReturnsSetWithFinalTopicName()
    {
        var ctx = new DummyContext();
        var options = new KsqlDslOptions();
        KafkaContextStateStoreExtensions.InitializeStateStores(ctx, options);
        var manager = ctx.GetStateStoreManager()!;

        var builder = new ModelBuilder();
        builder.Entity<Sample>()
            .WithTopic("orders")
            .HasKey(e => e.Id);
        var model = builder.GetEntityModel<Sample>()!;

        var baseSet = new Kafka.Ksql.Linq.StateStore.WindowedEntitySet<Sample>(ctx.Set<Sample>(), 5, (StateStoreManager)manager, model);
        var finalSet = baseSet.UseFinalized();

        Assert.Equal("orders_window_5_final", finalSet.GetTopicName());
    }
}
