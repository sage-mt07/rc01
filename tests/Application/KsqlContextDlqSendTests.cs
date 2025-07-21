using Kafka.Ksql.Linq;
using Kafka.Ksql.Linq.Configuration;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Messaging.Abstractions;
using Kafka.Ksql.Linq.Core.Models;
using Kafka.Ksql.Linq.Messaging.Producers;
using Kafka.Ksql.Linq.Messaging.Producers.Core;
using Kafka.Ksql.Linq.Core.Dlq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
#nullable enable

using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Application;

public class KsqlContextDlqSendTests
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
        public Task<KafkaDeliveryResult> DeleteAsync(object key, KafkaMessageContext? context = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new KafkaDeliveryResult());
        // Batch sending removed
        public Task FlushAsync(TimeSpan timeout) => Task.CompletedTask;
        public void Dispose() { }
    }

    private class TestContext : KsqlContext
    {
        public TestContext() : base(new KsqlDslOptions()) { }

        protected override bool SkipSchemaRegistration => true;

        public void SetProducerManager(KafkaProducerManager manager)
        {
            typeof(KsqlContext).GetField("_producerManager", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(this, manager);
        }

        public void SetDlqProducer(DlqProducer producer)
        {
            typeof(KsqlContext).GetField("_dlqProducer", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(this, producer);
        }
    }

    private class Sample { public int Id { get; set; } }

    [Fact]
    public async Task SendToDlqAsync_UsesDlqProducer()
    {
        var ctx = new TestContext();
        var options = new KsqlDslOptions();
        var manager = new KafkaProducerManager(Microsoft.Extensions.Options.Options.Create(options), null);
        ctx.SetProducerManager(manager);

        var stub = new StubProducer<DlqEnvelope>();
        var dict = (ConcurrentDictionary<(System.Type,string), object>)
            typeof(KafkaProducerManager).GetField("_topicProducers", BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(manager)!;
        dict[(typeof(DlqEnvelope), options.DlqTopicName)] = stub;

        var dlq = new DlqProducer(manager, new DlqOptions { TopicName = options.DlqTopicName });
        dlq.InitializeAsync().GetAwaiter().GetResult();
        ctx.SetDlqProducer(dlq);

        await ctx.SendToDlqAsync(new Sample(), new System.Exception("err"));

        Assert.True(stub.Sent);
    }
}
