using Confluent.Kafka;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Messaging.Producers.Core;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

#nullable enable

namespace Kafka.Ksql.Linq.Tests.Messaging.Producers.Core;

public class KafkaProducerTests
{
    private class DummySerializer : ISerializer<object>
    {
        public byte[] Serialize(object data, SerializationContext context) => Array.Empty<byte>();
    }
    private class DummyProducer : IProducer<object, object>
    {
        public List<(TopicPartition Partition, Message<object, object> Message)> Produced { get; } = new();
        public string Name => "dummy";
        public Handle Handle => throw new NotImplementedException();
        public int AddBrokers(string brokers) => 0;
        public void SetSaslCredentials(string username, string password) { }
        public void Dispose() { }
        public void Flush(CancellationToken cancellationToken = default) { }
        public int Flush(TimeSpan timeout) => 0;
        public void Produce(string topic, Message<object, object> message, Action<DeliveryReport<object, object>>? handler = null) => throw new NotImplementedException();
        public void Produce(TopicPartition topicPartition, Message<object, object> message, Action<DeliveryReport<object, object>>? handler = null)
        {
            Produced.Add((topicPartition, message));
            handler?.Invoke(new DeliveryReport<object, object>
            {
                Topic = topicPartition.Topic,
                Partition = topicPartition.Partition,
                Offset = new Offset(1),
                Message = message
            });
        }
        public int Poll(TimeSpan timeout) => 0;
        public Task<DeliveryResult<object, object>> ProduceAsync(string topic, Message<object, object> message, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<DeliveryResult<object, object>> ProduceAsync(TopicPartition topicPartition, Message<object, object> message, CancellationToken cancellationToken = default)
        {
            Produced.Add((topicPartition, message));
            return Task.FromResult(new DeliveryResult<object, object>
            {
                Topic = topicPartition.Topic,
                Partition = topicPartition.Partition,
                Offset = new Offset(1),
                Status = PersistenceStatus.Persisted,
                Message = message
            });
        }
        public void InitTransactions(TimeSpan timeout) { }
        public void BeginTransaction() { }
        public void CommitTransaction(TimeSpan timeout) { }
        public void CommitTransaction() { }
        public void AbortTransaction(TimeSpan timeout) { }
        public void AbortTransaction() { }
        public void SendOffsetsToTransaction(IEnumerable<TopicPartitionOffset> offsets, IConsumerGroupMetadata groupMetadata, TimeSpan timeout) { }
    }

    private class FailingProducer : IProducer<object, object>
    {
        public string Name => "fail";
        public Handle Handle => throw new NotImplementedException();
        public int AddBrokers(string brokers) => 0;
        public void SetSaslCredentials(string username, string password) { }
        public void Dispose() { }
        public void Flush(CancellationToken cancellationToken = default) { }
        public int Flush(TimeSpan timeout) => 0;
        public void Produce(string topic, Message<object, object> message, Action<DeliveryReport<object, object>>? handler = null) => throw new NotImplementedException();
        public void Produce(TopicPartition topicPartition, Message<object, object> message, Action<DeliveryReport<object, object>>? handler = null) => throw new NotImplementedException();
        public int Poll(TimeSpan timeout) => 0;
        public Task<DeliveryResult<object, object>> ProduceAsync(string topic, Message<object, object> message, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("fail");
        public Task<DeliveryResult<object, object>> ProduceAsync(TopicPartition topicPartition, Message<object, object> message, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("fail");
        public void InitTransactions(TimeSpan timeout) { }
        public void BeginTransaction() { }
        public void CommitTransaction(TimeSpan timeout) { }
        public void CommitTransaction() { }
        public void AbortTransaction(TimeSpan timeout) { }
        public void AbortTransaction() { }
        public void SendOffsetsToTransaction(IEnumerable<TopicPartitionOffset> offsets, IConsumerGroupMetadata groupMetadata, TimeSpan timeout) { }
    }

    private class Sample
    {
        public int Id { get; set; }
    }

    private static EntityModel CreateModel()
    {
        return new EntityModel
        {
            EntityType = typeof(Sample),
            TopicName = "orders",
            AllProperties = typeof(Sample).GetProperties(),
            KeyProperties = new[] { typeof(Sample).GetProperty(nameof(Sample.Id))! }
        };
    }

    [Fact]
    public async Task SendAsync_ProducesMessage()
    {
        var producer = new DummyProducer();
        var kp = new KafkaProducer<Sample>(producer, new DummySerializer(), new DummySerializer(), "orders", CreateModel(), NullLoggerFactory.Instance);

        var entity = new Sample { Id = 1 };
        var result = await kp.SendAsync(entity);

        Assert.Single(producer.Produced);
        Assert.Equal("orders", producer.Produced[0].Partition.Topic);
        Assert.Equal(1, result.Offset);
    }

    [Fact]
    public async Task SendAsync_RaisesSendErrorEvent()
    {
        var producer = new FailingProducer();
        var kp = new KafkaProducer<Sample>(producer, new DummySerializer(), new DummySerializer(), "orders", CreateModel(), NullLoggerFactory.Instance);

        var raised = false;
        kp.SendError += (msg, ctx, ex) => { raised = true; return Task.CompletedTask; };

        await Assert.ThrowsAsync<InvalidOperationException>(() => kp.SendAsync(new Sample { Id = 2 }));

        Assert.True(raised);
    }
}
