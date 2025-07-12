using Kafka.Ksql.Linq.Application;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Core.Context;
using Kafka.Ksql.Linq.Core.Modeling;
using Kafka.Ksql.Linq.Mapping;
using Kafka.Ksql.Linq.Messaging.Abstractions;
using Kafka.Ksql.Linq.Messaging.Producers.Core;
using Kafka.Ksql.Linq.Configuration;
using Kafka.Ksql.Linq.Query.Abstractions;
using System.Linq;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Mapping;

public class AutomaticQueryFlowTests
{
    private record User(int Id, string Name);

    private class StubProducer<T> : IKafkaProducer<T> where T : class
    {
        public bool Sent;
        public string TopicName => "users";
        public Task<KafkaDeliveryResult> SendAsync(T message, KafkaMessageContext? context = null, CancellationToken cancellationToken = default)
        {
            Sent = true;
            return Task.FromResult(new KafkaDeliveryResult());
        }
        public Task<KafkaBatchDeliveryResult> SendBatchAsync(IEnumerable<T> messages, KafkaMessageContext? context = null, CancellationToken cancellationToken = default)
        {
            Sent = true;
            return Task.FromResult(new KafkaBatchDeliveryResult());
        }
        public Task FlushAsync(System.TimeSpan timeout) => Task.CompletedTask;
        public void Dispose() { }
    }

    private class TestContext : KsqlContext
    {
        public TestContext() : base() { }
        protected override bool SkipSchemaRegistration => true;
        public void SetProducer(object manager)
        {
            typeof(KsqlContext).GetField("_producerManager", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(this, manager);
        }
        protected override void OnModelCreating(IModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>()
                .WithTopic("users")
                .HasKey(u => u.Id);
        }
    }

    [Fact]
    public async Task Query_To_AddAsync_SendsMessage()
    {
        var ctx = new TestContext();
        var manager = new KafkaProducerManager(Options.Create(new KsqlDslOptions()), null);
        ctx.SetProducer(manager);
        var stub = new StubProducer<User>();
        var dict = (ConcurrentDictionary<System.Type, object>)typeof(KafkaProducerManager)
            .GetField("_producers", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(manager)!;
        dict[typeof(User)] = stub;

        var entityModels = ctx.GetEntityModels();
        var mapping = new MappingManager();
        mapping.Register<User>(entityModels[typeof(User)]);

        var builder = new QueryBuilder<User>()
            .FromSource<User>(src => src.Where(u => u.Id == 1));
        var schema = builder.GetSchema();
        Assert.True(schema.IsValid);

        var user = new User(1, "Alice");
        var (key, value) = mapping.ExtractKeyValue(user);

        Assert.Equal(user.Id, key);
        Assert.Same(user, value);

        await ctx.Set<User>().AddAsync(user);

        Assert.True(stub.Sent);
    }
}
