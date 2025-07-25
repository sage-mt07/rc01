using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using Confluent.Kafka;
using Confluent.SchemaRegistry;
using Kafka.Ksql.Linq.Configuration;
using Kafka.Ksql.Linq.Configuration.Abstractions;
using Kafka.Ksql.Linq.Messaging.Configuration;
using Kafka.Ksql.Linq.Core.Configuration;
using Kafka.Ksql.Linq.Messaging.Producers;
using Kafka.Ksql.Linq.Messaging.Consumers;
using Kafka.Ksql.Linq.Core.Dlq;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using static Kafka.Ksql.Linq.Tests.PrivateAccessor;
using Xunit;

#nullable enable

namespace Kafka.Ksql.Linq.Tests.Messaging;

public class KafkaConsumerManagerTests
{
    private class SampleEntity
    {
        public int Id { get; set; }
    }

    [Fact]
    public void BuildConsumerConfig_ReturnsConfiguredValues()
    {
        var options = new KsqlDslOptions
        {
            Common = new CommonSection { BootstrapServers = "server", ClientId = "cid" },
            Topics = new Dictionary<string, TopicSection>
            {
                ["topic"] = new TopicSection
                {
                    Consumer = new ConsumerSection
                    {
                        GroupId = "gid",
                        AutoOffsetReset = "Earliest",
                        EnableAutoCommit = false,
                        AutoCommitIntervalMs = 100,
                        SessionTimeoutMs = 200,
                        HeartbeatIntervalMs = 300,
                        MaxPollIntervalMs = 400,
                        FetchMinBytes = 5,
                        FetchMaxBytes = 10,
                        IsolationLevel = "ReadCommitted",
                        AdditionalProperties = new Dictionary<string,string>{{"p","v"}}
                    }
                }
            }
        };
        var producerManager = new KafkaProducerManager(Options.Create(options), new NullLoggerFactory());
        var dlqProducer = new DlqProducer(producerManager, new DlqOptions { TopicName = options.DlqTopicName });
        var manager = new KafkaConsumerManager(
            Options.Create(options),
            new NullLoggerFactory());
        manager.DeserializationError += (data, ex, topic, part, off, ts, headers, keyType, valueType) =>
            dlqProducer.SendAsync(data, ex, topic, part, off, ts, headers, keyType, valueType);
        var config = InvokePrivate<ConsumerConfig>(manager, "BuildConsumerConfig", new[] { typeof(string), typeof(KafkaSubscriptionOptions) }, null, "topic", null);

        Assert.Equal("server", config.BootstrapServers);
        Assert.Equal("cid", config.ClientId);
        Assert.Equal("gid", config.GroupId);
        Assert.Equal(AutoOffsetReset.Earliest, config.AutoOffsetReset);
        Assert.False(config.EnableAutoCommit);
        Assert.Equal(100, config.AutoCommitIntervalMs);
        Assert.Equal(200, config.SessionTimeoutMs);
        Assert.Equal(300, config.HeartbeatIntervalMs);
        Assert.Equal(400, config.MaxPollIntervalMs);
        Assert.Equal(5, config.FetchMinBytes);
        Assert.Equal(10, config.FetchMaxBytes);
        Assert.Equal(IsolationLevel.ReadCommitted, config.IsolationLevel);
        Assert.Equal("v", config.Get("p"));
    }


    [Fact]
    public void GetEntityModel_ReturnsModelWithAttributes()
    {
        var options = Options.Create(new KsqlDslOptions());
        var producerManager = new KafkaProducerManager(options, new NullLoggerFactory());
        var dlqProducer = new DlqProducer(producerManager, new DlqOptions { TopicName = options.Value.DlqTopicName });
        var manager = new KafkaConsumerManager(
            options,
            new NullLoggerFactory());
        manager.DeserializationError += (data, ex, topic, part, off, ts, headers, keyType, valueType) =>
            dlqProducer.SendAsync(data, ex, topic, part, off, ts, headers, keyType, valueType);

        var model = InvokePrivate<Kafka.Ksql.Linq.Core.Abstractions.EntityModel>(manager, "GetEntityModel", Type.EmptyTypes, new[] { typeof(SampleEntity) });
        Assert.Equal(typeof(SampleEntity), model.EntityType);
        Assert.Empty(model.KeyProperties);
        Assert.Equal("sampleentity", model.TopicName);
    }

    [Fact]
    public void DeserializerCaching_WorksPerType()
    {
        var optionsForCache = new KsqlDslOptions
        {
            SchemaRegistry = new SchemaRegistrySection { Url = "http://example" }
        };
        var manager = new KafkaConsumerManager(Options.Create(optionsForCache), new NullLoggerFactory());
        var d1 = InvokePrivate<IDeserializer<object>>(manager, "CreateKeyDeserializer", new[] { typeof(Type) }, null, typeof(int));
        var d2 = InvokePrivate<IDeserializer<object>>(manager, "CreateKeyDeserializer", new[] { typeof(Type) }, null, typeof(int));
        var cache = (ConcurrentDictionary<Type, IDeserializer<object>>)typeof(KafkaConsumerManager)
            .GetField("_keyDeserializerCache", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(manager)!;
        Assert.Same(d1, d2);
        Assert.Single(cache);

        var v1 = InvokePrivate<IDeserializer<object>>(manager, "GetValueDeserializer", Type.EmptyTypes, new[] { typeof(string) });
        var v2 = InvokePrivate<IDeserializer<object>>(manager, "GetValueDeserializer", Type.EmptyTypes, new[] { typeof(string) });
        var vcache = (ConcurrentDictionary<Type, IDeserializer<object>>)typeof(KafkaConsumerManager)
            .GetField("_valueDeserializerCache", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(manager)!;
        Assert.Same(v1, v2);
        Assert.Single(vcache);
    }
}
