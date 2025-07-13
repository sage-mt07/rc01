using Kafka.Ksql.Linq.Application;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Mapping;
using Kafka.Ksql.Linq.Messaging.Abstractions;
using Kafka.Ksql.Linq.Messaging.Producers.Core;
using Kafka.Ksql.Linq.Configuration;
using Kafka.Ksql.Linq.Entities.Samples.Models;
using SampleOrder = Kafka.Ksql.Linq.Entities.Samples.Models.Order;
using Kafka.Ksql.Linq.Entities.Samples;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Mapping;

public class AddAsyncSampleFlowTests
{
    private class StubProducer<T> : IKafkaProducer<T> where T : class
    {
        public bool Sent;
        public string TopicName => "orders";
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
        public void SetProducerManager(KafkaProducerManager manager)
        {
            typeof(KsqlContext).GetField("_producerManager", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(this, manager);
        }
        protected override void OnModelCreating(IModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SampleOrder>()
                .WithTopic("orders")
                .HasKey(o => new { o.OrderId, o.UserId });
        }
    }

    [Fact]
    public async Task AddAsync_Flow_SendsMessage()
    {
        var services = new ServiceCollection();
        services.AddSampleModels();
        services.AddSingleton<TestContext>();
        var provider = services.BuildServiceProvider();
        var ctx = provider.GetRequiredService<TestContext>();
        var mapping = provider.GetRequiredService<IMappingManager>();

        var manager = new KafkaProducerManager(Options.Create(new KsqlDslOptions()), null);
        ctx.SetProducerManager(manager);
        var stub = new StubProducer<SampleOrder>();
        var dict = (ConcurrentDictionary<System.Type, object>)typeof(KafkaProducerManager)
            .GetField("_producers", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(manager)!;
        dict[typeof(SampleOrder)] = stub;

        var order = new SampleOrder { OrderId = 1, UserId = 10, ProductId = 5, Quantity = 2 };
        var (key, value) = mapping.ExtractKeyValue(order);
        Assert.IsType<Dictionary<string, object>>(key);

        await ctx.Set<SampleOrder>().AddAsync(order);

        Assert.True(stub.Sent);
    }
}
