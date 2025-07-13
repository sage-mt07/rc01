using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Confluent.Kafka;
using Kafka.Ksql.Linq.Configuration;
using Kafka.Ksql.Linq.Configuration.Abstractions;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Messaging.Consumers.Core;
using Kafka.Ksql.Linq.Messaging.Producers;
using Kafka.Ksql.Linq.Messaging.Producers.Core;
using Kafka.Ksql.Linq.Core.Dlq;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
#nullable enable

namespace Kafka.Ksql.Linq.Tests.Messaging.Consumers;

public class KafkaConsumerMessageCreationTests
{
    private class FakeConsumer : DispatchProxy
    {
        public Queue<ConsumeResult<object, object>?> Queue { get; } = new();

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            switch (targetMethod?.Name)
            {
                case nameof(IConsumer<object, object>.Subscribe):
                    return null;
                case nameof(IConsumer<object, object>.Consume) when args?.Length == 1 && args[0] is TimeSpan:
                    if (Queue.Count == 0)
                        return null;
                    return Queue.Dequeue();
                case nameof(IConsumer<object, object>.Unsubscribe):
                case nameof(IConsumer<object, object>.Close):
                case nameof(IDisposable.Dispose):
                    return null;
            }
            throw new NotImplementedException(targetMethod?.Name);
        }
    }

    private delegate object? DeserializeHandler(ReadOnlySpan<byte> data, bool isNull, SerializationContext context);

    private class StubDeserializer : IDeserializer<object>
    {
        public DeserializeHandler Handler { get; set; } = (_, _, _) => null;
        public object Deserialize(ReadOnlySpan<byte> data, bool isNull, SerializationContext context) => Handler(data, isNull, context)!;
    }

    private static EntityModel CreateModel() => new()
    {
        EntityType = typeof(TestEntity),
        TopicName = "t",
        KeyProperties = new[] { typeof(TestEntity).GetProperty(nameof(TestEntity.Id))! },
        AllProperties = typeof(TestEntity).GetProperties()
    };

    private static KafkaConsumer<TestEntity, int> CreateConsumer(FakeConsumer fake, StubDeserializer keyDeser, StubDeserializer valDeser)
    {
        var options = Options.Create(new KsqlDslOptions());
        var prodMgr = new KafkaProducerManager(options, new NullLoggerFactory());
        var dlq = new DlqProducer(prodMgr, new DlqOptions());
        return new KafkaConsumer<TestEntity, int>(
            (IConsumer<object, object>)fake,
            keyDeser,
            valDeser,
            "t",
            CreateModel(),
            DeserializationErrorPolicy.Skip,
            "dlq",
            dlq,
            new NullLoggerFactory());
    }

    [Fact]
    public async Task ConsumeBatchAsync_WithValidMessage_ReturnsKafkaMessage()
    {
        var fake = DispatchProxy.Create<IConsumer<object, object>, FakeConsumer>() as FakeConsumer;
        var headers = new Headers { new Header("correlationId", System.Text.Encoding.UTF8.GetBytes("cid")) };
        var msg = new Message<object, object> { Key = new byte[] {1}, Value = new byte[] {2}, Timestamp = new Timestamp(DateTime.UtcNow), Headers = headers };
        fake!.Queue.Enqueue(new ConsumeResult<object, object>
        {
            Message = msg,
            Topic = "t",
            Partition = new Partition(0),
            Offset = new Offset(0)
        });
        TestEntity entity = new() { Id = 99 };
        var keyDeser = new StubDeserializer { Handler = (_,_,_) => 10 };
        var valDeser = new StubDeserializer { Handler = (_,_,_) => entity };
        var consumer = CreateConsumer(fake, keyDeser, valDeser);
        var opts = new KafkaBatchOptions { MaxBatchSize = 1, MaxWaitTime = TimeSpan.FromSeconds(1) };

        var batch = await consumer.ConsumeBatchAsync(opts);

        Assert.Single(batch.Messages);
        var result = batch.Messages[0];
        Assert.Same(entity, result.Value); // merged on same instance
        Assert.Equal(10, result.Value.Id); // key merged into entity
        Assert.Equal(10, result.Key);
        Assert.Same(headers, result.Headers);
        Assert.Equal("cid", result.Context?.CorrelationId);
        Assert.Equal("t", result.Topic);
        Assert.Equal(0, result.Partition);
        Assert.Equal(0, result.Offset);
    }

    [Fact]
    public async Task ConsumeBatchAsync_WhenValueDeserializerReturnsNull_DropsMessage()
    {
        var fake = DispatchProxy.Create<IConsumer<object, object>, FakeConsumer>() as FakeConsumer;
        var msg = new Message<object, object> { Key = new byte[] {1}, Value = null!, Timestamp = new Timestamp(DateTime.UtcNow) };
        fake!.Queue.Enqueue(new ConsumeResult<object, object>
        {
            Message = msg,
            Topic = "t",
            Partition = new Partition(0),
            Offset = new Offset(0)
        });
        var keyDeser = new StubDeserializer { Handler = (_,_,_) => 1 };
        var valDeser = new StubDeserializer { Handler = (_,_,_) => null };
        var consumer = CreateConsumer(fake, keyDeser, valDeser);
        var opts = new KafkaBatchOptions { MaxBatchSize = 1, MaxWaitTime = TimeSpan.FromSeconds(1) };

        var batch = await consumer.ConsumeBatchAsync(opts);

        Assert.Empty(batch.Messages);
    }

    [Fact]
    public async Task ConsumeBatchAsync_WithHeaders_SetsCorrelationId()
    {
        var fake = DispatchProxy.Create<IConsumer<object, object>, FakeConsumer>() as FakeConsumer;
        var headers = new Headers { new Header("correlationId", System.Text.Encoding.UTF8.GetBytes("abc")) };
        var msg = new Message<object, object> { Key = new byte[] {1}, Value = new byte[] {2}, Headers = headers, Timestamp = new Timestamp(DateTime.UtcNow) };
        fake!.Queue.Enqueue(new ConsumeResult<object, object>
        {
            Message = msg,
            Topic = "t",
            Partition = new Partition(0),
            Offset = new Offset(1)
        });
        var keyDeser = new StubDeserializer { Handler = (_,_,_) => 5 };
        var valDeser = new StubDeserializer { Handler = (_,_,_) => new TestEntity() };
        var consumer = CreateConsumer(fake, keyDeser, valDeser);
        var opts = new KafkaBatchOptions { MaxBatchSize = 1, MaxWaitTime = TimeSpan.FromSeconds(1) };

        var batch = await consumer.ConsumeBatchAsync(opts);

        Assert.Single(batch.Messages);
        var result = batch.Messages[0];
        Assert.Same(headers, result.Headers);
        Assert.Equal("abc", result.Context?.CorrelationId);
    }
}
