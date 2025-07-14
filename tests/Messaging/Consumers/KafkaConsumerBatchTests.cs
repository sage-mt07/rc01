using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
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

public class KafkaConsumerBatchTests
{
    private class FakeConsumer : DispatchProxy
    {
        public Queue<ConsumeResult<object, object>?> Queue { get; } = new();
        public bool ThrowOnConsume { get; set; }
        public Exception? ExceptionToThrow { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            switch (targetMethod?.Name)
            {
                case nameof(IConsumer<object, object>.Subscribe):
                    return null;
                case nameof(IConsumer<object, object>.Consume) when args?.Length == 1 && args[0] is TimeSpan:
                    if (ExceptionToThrow != null)
                        throw ExceptionToThrow;
                    if (ThrowOnConsume)
                        throw new InvalidOperationException("consume");
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

    private delegate object? DeserHandler(ReadOnlySpan<byte> data, bool isNull, SerializationContext context);

    private class StubDeserializer : IDeserializer<object>
    {
        public DeserHandler Handler { get; set; } = (_, _, _) => null;
        public object Deserialize(ReadOnlySpan<byte> data, bool isNull, SerializationContext context)
            => Handler(data, isNull, context)!;
    }

    private static EntityModel CreateModel() => new()
    {
        EntityType = typeof(TestEntity),
        TopicName = "t",
        KeyProperties = new[] { typeof(TestEntity).GetProperty(nameof(TestEntity.Id))! },
        AllProperties = typeof(TestEntity).GetProperties()
    };

    private static KafkaConsumer<TestEntity, int> CreateConsumer(FakeConsumer fake)
    {
        var keyDeser = new StubDeserializer { Handler = (_, _, _) => 0 };
        var valDeser = new StubDeserializer { Handler = (_, _, _) => new TestEntity() };
        var options = Options.Create(new KsqlDslOptions());
        var prodMgr = new KafkaProducerManager(options, new NullLoggerFactory());
        var dlq = new DlqProducer(prodMgr, new DlqOptions());
        var consumer = new KafkaConsumer<TestEntity, int>(
            (IConsumer<object, object>)fake,
            keyDeser,
            valDeser,
            "t",
            CreateModel(),
            DeserializationErrorPolicy.Skip,
            new NullLoggerFactory());
        consumer.DeserializationError += (data, ex, topic, part, off, ts, headers, keyType, valueType) =>
            dlq.SendAsync(data, ex, topic, part, off, ts, headers, keyType, valueType);
        return consumer;
    }

    [Fact]
    public async Task ConsumeBatchAsync_ReturnsSpecifiedNumber()
    {
        var fake = DispatchProxy.Create<IConsumer<object, object>, FakeConsumer>() as FakeConsumer;
        var now = DateTime.UtcNow;
        for (int i = 0; i < 3; i++)
        {
            var msg = new Message<object, object> { Key = i, Value = i, Timestamp = new Timestamp(now) };
            fake!.Queue.Enqueue(new ConsumeResult<object, object>
            {
                Message = msg,
                Topic = "t",
                Partition = new Partition(0),
                Offset = new Offset(i)
            });
        }
        var consumer = CreateConsumer(fake!);
        var opts = new KafkaBatchOptions { MaxBatchSize = 3, MaxWaitTime = TimeSpan.FromSeconds(1) };
        var batch = await consumer.ConsumeBatchAsync(opts);
        Assert.Equal(3, batch.Messages.Count);
    }

    [Fact]
    public async Task ConsumeBatchAsync_WhenConsumeThrows_Rethrows()
    {
        var fake = DispatchProxy.Create<IConsumer<object, object>, FakeConsumer>() as FakeConsumer;
        fake!.ThrowOnConsume = true;
        var consumer = CreateConsumer(fake);
        var opts = new KafkaBatchOptions { MaxBatchSize = 1, MaxWaitTime = TimeSpan.FromMilliseconds(100) };
        await Assert.ThrowsAsync<InvalidOperationException>(() => consumer.ConsumeBatchAsync(opts));
    }

    [Fact]
    public async Task ConsumeBatchAsync_WhenTimeoutOccurs_ShouldReturnEmptyBatch()
    {
        var fake = DispatchProxy.Create<IConsumer<object, object>, FakeConsumer>() as FakeConsumer;
        var consumer = CreateConsumer(fake!);
        var opts = new KafkaBatchOptions { MaxBatchSize = 5, MaxWaitTime = TimeSpan.FromMilliseconds(50) };
        var batch = await consumer.ConsumeBatchAsync(opts);
        Assert.Empty(batch.Messages);
    }

    [Fact]
    public async Task ConsumeBatchAsync_WhenKafkaDisconnects_ShouldThrowKafkaException()
    {
        var fake = DispatchProxy.Create<IConsumer<object, object>, FakeConsumer>() as FakeConsumer;
        fake!.ExceptionToThrow = new KafkaException(new Error(ErrorCode.Local_Transport));
        var consumer = CreateConsumer(fake);
        var opts = new KafkaBatchOptions { MaxBatchSize = 1, MaxWaitTime = TimeSpan.FromMilliseconds(100) };
        await Assert.ThrowsAsync<KafkaException>(() => consumer.ConsumeBatchAsync(opts));
    }
}
