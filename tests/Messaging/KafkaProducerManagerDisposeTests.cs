using Kafka.Ksql.Linq.Configuration;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Messaging.Abstractions;
using Kafka.Ksql.Linq.Messaging.Configuration;
using Kafka.Ksql.Linq.Messaging.Producers;
using Kafka.Ksql.Linq.Messaging.Producers.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Confluent.SchemaRegistry;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Messaging;
#nullable enable

public class KafkaProducerManagerDisposeTests
{
    private class Sample { }

    private class StubProducer<T> : IKafkaProducer<T> where T : class
    {
        public bool Disposed;
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
        public void Dispose() { Disposed = true; }
    }



    private static ConcurrentDictionary<Type, object> GetProducerDict(KafkaProducerManager manager)
        => (ConcurrentDictionary<Type, object>)typeof(KafkaProducerManager)
            .GetField("_producers", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(manager)!;

    private static ConcurrentDictionary<(Type, string), object> GetTopicProducerDict(KafkaProducerManager manager)
        => (ConcurrentDictionary<(Type, string), object>)typeof(KafkaProducerManager)
            .GetField("_topicProducers", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(manager)!;


    private static Lazy<Confluent.SchemaRegistry.ISchemaRegistryClient> GetSchemaLazy(KafkaProducerManager manager)
        => (Lazy<Confluent.SchemaRegistry.ISchemaRegistryClient>)typeof(KafkaProducerManager)
            .GetField("_schemaRegistryClient", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(manager)!;

    private static bool GetDisposedFlag(KafkaProducerManager manager)
        => (bool)typeof(KafkaProducerManager)
            .GetField("_disposed", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(manager)!;

    [Fact]
    public void Dispose_BeforeProducerCreation_DoesNotThrow()
    {
        var manager = new KafkaProducerManager(Options.Create(new KsqlDslOptions()), new NullLoggerFactory());
        manager.Dispose();

        Assert.True(GetDisposedFlag(manager));
        Assert.Empty(GetProducerDict(manager));
        Assert.Empty(GetTopicProducerDict(manager));
        Assert.False(GetSchemaLazy(manager).IsValueCreated);
    }

    [Fact]
    public void Dispose_WithCachedResources_DisposesAll()
    {
        var manager = new KafkaProducerManager(Options.Create(new KsqlDslOptions()), new NullLoggerFactory());
        var producers = GetProducerDict(manager);
        var topics = GetTopicProducerDict(manager);
        var schemaLazyField = typeof(KafkaProducerManager).GetField("_schemaRegistryClient", BindingFlags.NonPublic | BindingFlags.Instance)!;
        schemaLazyField.SetValue(manager, new Lazy<ISchemaRegistryClient>(() => new CachedSchemaRegistryClient(new SchemaRegistryConfig { Url = "http://localhost" })));

        var p1 = new StubProducer<Sample>();
        var p2 = new StubProducer<Sample>();
        producers[typeof(Sample)] = p1;
        topics[(typeof(Sample), "t")] = p2;

        manager.Dispose();

        Assert.True(p1.Disposed);
        Assert.True(p2.Disposed);
        Assert.Empty(producers);
        Assert.Empty(topics);
        Assert.True(GetDisposedFlag(manager));
    }

    [Fact]
    public async Task Dispose_AfterUse_DisposesProducers()
    {
        var manager = new KafkaProducerManager(Options.Create(new KsqlDslOptions()), new NullLoggerFactory());
        var producers = GetProducerDict(manager);
        var stub = new StubProducer<Sample>();
        producers[typeof(Sample)] = stub;

        await manager.SendAsync(new Sample());
        Assert.True(stub.Sent);

        manager.Dispose();
        Assert.True(stub.Disposed);
        Assert.True(GetDisposedFlag(manager));
    }
}
