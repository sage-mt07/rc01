using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Messaging.Abstractions;
using Kafka.Ksql.Linq.Messaging.Producers.Core;
using Kafka.Ksql.Linq.Configuration;
using Kafka.Ksql.Linq.Configuration.Abstractions;
using Kafka.Ksql.Linq.Messaging.Consumers;
using Kafka.Ksql.Linq.Messaging.Producers;
using Kafka.Ksql.Linq.Core.Dlq;
using Kafka.Ksql.Linq.Messaging.Configuration;
using Kafka.Ksql.Linq.Core.Configuration;
using Microsoft.Extensions.Options;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
#nullable enable

namespace Kafka.Ksql.Linq.Tests.Infrastructure.Consumer;

public class KafkaConsumerManagerTests
{
    private class SampleEntity
    {
        public int Id { get; set; }
    }

    private class StubConsumer : IKafkaConsumer<SampleEntity, object>
    {
        private readonly Func<CancellationToken, IAsyncEnumerable<KafkaMessage<SampleEntity, object>>> _consume;
        public string TopicName { get; }
        public StubConsumer(string topic, Func<CancellationToken, IAsyncEnumerable<KafkaMessage<SampleEntity, object>>> consume)
        {
            TopicName = topic;
            _consume = consume;
        }
        public IAsyncEnumerable<KafkaMessage<SampleEntity, object>> ConsumeAsync(CancellationToken cancellationToken = default) => _consume(cancellationToken);
        public Task<KafkaBatch<SampleEntity, object>> ConsumeBatchAsync(KafkaBatchOptions options, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task CommitAsync(TopicPartitionOffset offset) => Task.CompletedTask;
        public Task SeekAsync(TopicPartitionOffset offset) => Task.CompletedTask;
        public List<TopicPartition> GetAssignedPartitions() => new();
        public void Dispose() { }
    }

    private static KafkaConsumerManager CreateManager(IKafkaConsumer<SampleEntity, object> consumer, ILogger? logger)
    {
        var options = Options.Create(new KsqlDslOptions());
        var producerManager = new KafkaProducerManager(options, new NullLoggerFactory());
        var dlqProducer = new DlqProducer(producerManager, new DlqOptions { TopicName = options.Value.DlqTopicName });
        var manager = new KafkaConsumerManager(
            options,
            new NullLoggerFactory());
        manager.DeserializationError += (data, ex, topic, part, off, ts, headers, keyType, valueType) =>
            dlqProducer.SendAsync(data, ex, topic, part, off, ts, headers, keyType, valueType);

        // inject stub consumer via reflection
        typeof(KafkaConsumerManager).GetField("_consumers", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(manager, new ConcurrentDictionary<Type, object>(new[] { new KeyValuePair<Type, object>(typeof(SampleEntity), consumer) }));
        typeof(KafkaConsumerManager).GetField("_logger", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(manager, logger);

        return manager;
    }

    [Fact]
    public async Task SubscribeAsync_CallsHandlerOnce()
    {
        async IAsyncEnumerable<KafkaMessage<SampleEntity, object>> OneMessage([EnumeratorCancellation] CancellationToken token)
        {
            await Task.Yield();
            yield return new KafkaMessage<SampleEntity, object> { Value = new SampleEntity { Id = 1 } };
        }
        var consumer = new StubConsumer("topic", OneMessage);
        var manager = CreateManager(consumer, NullLogger.Instance);
        var tcs = new TaskCompletionSource<bool>();
        int count = 0;

        await manager.SubscribeAsync<SampleEntity>(async (e, ctx) => { count++; tcs.SetResult(true); await Task.CompletedTask; }, cancellationToken: CancellationToken.None);
        await Task.WhenAny(tcs.Task, Task.Delay(1000));
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task SubscribeAsync_HandlerThrows_LogsAndContinues()
    {
        async IAsyncEnumerable<KafkaMessage<SampleEntity, object>> TwoMessages([EnumeratorCancellation] CancellationToken token)
        {
            await Task.Yield();
            yield return new KafkaMessage<SampleEntity, object> { Value = new SampleEntity { Id = 1 } };
            yield return new KafkaMessage<SampleEntity, object> { Value = new SampleEntity { Id = 2 } };
        }
        var consumer = new StubConsumer("topic", TwoMessages);
        var loggerMock = new Mock<ILogger>();
        var manager = CreateManager(consumer, loggerMock.Object);
        var tcs = new TaskCompletionSource<bool>();
        int count = 0;

        await manager.SubscribeAsync<SampleEntity>((e, ctx) =>
        {
            count++;
            if (count == 1) throw new InvalidOperationException("fail");
            tcs.SetResult(true);
            return Task.CompletedTask;
        }, cancellationToken: CancellationToken.None);

        await Task.WhenAny(tcs.Task, Task.Delay(1000));

        loggerMock.Verify(l => l.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.AtLeastOnce);
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task SubscribeAsync_ConsumeError_LogsAndStops()
    {
        async IAsyncEnumerable<KafkaMessage<SampleEntity, object>> Throwing([EnumeratorCancellation] CancellationToken token)
        {
            await Task.Yield();
            throw new Exception("consume error");
#pragma warning disable CS0162 // yield break is unreachable but required for async iterator
            yield break;
#pragma warning restore CS0162
        }
        var consumer = new StubConsumer("topic", Throwing);
        var loggerMock = new Mock<ILogger>();
        var manager = CreateManager(consumer, loggerMock.Object);

        await manager.SubscribeAsync<SampleEntity>((e, ctx) => Task.CompletedTask, cancellationToken: CancellationToken.None);
        await Task.Delay(100); // allow background task to run

        loggerMock.Verify(l => l.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task SubscribeAsync_Cancellation_StopsLoop()
    {
        async IAsyncEnumerable<KafkaMessage<SampleEntity, object>> Infinite([EnumeratorCancellation] CancellationToken token)
        {
            int i = 0;
            while (true)
            {
                token.ThrowIfCancellationRequested();
                yield return new KafkaMessage<SampleEntity, object> { Value = new SampleEntity { Id = i++ } };
                await Task.Delay(10, token);
            }
        }
        var consumer = new StubConsumer("topic", Infinite);
        var manager = CreateManager(consumer, NullLogger.Instance);
        var cts = new CancellationTokenSource();
        var tcs = new TaskCompletionSource<bool>();
        int count = 0;

        await manager.SubscribeAsync<SampleEntity>(async (e, ctx) =>
        {
            count++;
            tcs.SetResult(true);
            cts.Cancel();
            await Task.CompletedTask;
        }, cancellationToken: cts.Token);

        await Task.WhenAny(tcs.Task, Task.Delay(1000));
        await Task.Delay(100); // give time for cancellation
        Assert.Equal(1, count);
    }
}
