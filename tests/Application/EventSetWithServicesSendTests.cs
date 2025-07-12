using Kafka.Ksql.Linq;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Core.Context;
using Kafka.Ksql.Linq.Messaging.Abstractions;
using Kafka.Ksql.Linq.Messaging.Producers.Core;
using Kafka.Ksql.Linq.Configuration;
using Kafka.Ksql.Linq.Application;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System;

#nullable enable
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Application;

public class EventSetWithServicesSendTests
{
    private class StubProducer<T> : IKafkaProducer<T> where T : class
    {
        public bool Sent;
        public string TopicName => "t";
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
        public Task FlushAsync(TimeSpan timeout) => Task.CompletedTask;
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
    }

    private class Sample { public int Id { get; set; } }

    private static EntityModel CreateModel() => new()
    {
        EntityType = typeof(Sample),
        TopicName = "t",
        AllProperties = typeof(Sample).GetProperties(),
        KeyProperties = new[] { typeof(Sample).GetProperty(nameof(Sample.Id))! }
    };

    [Fact]
    public async Task SendEntityAsync_UsesProducerManager()
    {
        var ctx = new TestContext();
        var manager = new KafkaProducerManager(
            Microsoft.Extensions.Options.Options.Create(new KsqlDslOptions()),
            null);
        ctx.SetProducer(manager);

        var stub = new StubProducer<Sample>();
        var dict = (System.Collections.Concurrent.ConcurrentDictionary<Type, object>)
            typeof(KafkaProducerManager).GetField("_producers", BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(manager)!;
        dict[typeof(Sample)] = stub;

        var set = new EventSetWithServices<Sample>(ctx, CreateModel());
        await set.AddAsync(new Sample(), CancellationToken.None);
        Assert.True(stub.Sent);
    }
}
