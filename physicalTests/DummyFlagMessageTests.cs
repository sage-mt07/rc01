using Confluent.Kafka;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Messaging.Producers.Core;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Integration;

public class DummyFlagMessageTests
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
        public Task<DeliveryResult<object, object>> ProduceAsync(string topic, Message<object, object> message, CancellationToken cancellationToken = default) => throw new NotImplementedException();
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

    public class OrderValue
    {
        public int CustomerId { get; set; }
        public int Id { get; set; }
        public string? Region { get; set; }
        public double Amount { get; set; }
        public bool IsHighPriority { get; set; }
        public int Count { get; set; }
    }

    private static EntityModel CreateModel()
    {
        return new EntityModel
        {
            EntityType = typeof(OrderValue),
            TopicName = "orders",
            AllProperties = typeof(OrderValue).GetProperties(),
            KeyProperties = new[]
            {
                typeof(OrderValue).GetProperty(nameof(OrderValue.CustomerId))!,
                typeof(OrderValue).GetProperty(nameof(OrderValue.Id))!
            }
        };
    }

    [KsqlDbFact]
    public async Task SendAsync_AddsDummyFlagHeader()
    {
        await TestEnvironment.ResetAsync();

        var producer = new DummyProducer();
        var kp = new KafkaProducer<OrderValue>(producer, new DummySerializer(), new DummySerializer(), "orders", CreateModel(), NullLoggerFactory.Instance);

        var context = new KafkaMessageContext
        {
            Headers = new Dictionary<string, object> { ["is_dummy"] = true }
        };

        var entity = new OrderValue { CustomerId = 1, Id = 1, Region = "west", Amount = 10d, IsHighPriority = false, Count = 1 };
        await kp.SendAsync(entity, context);

        Assert.Single(producer.Produced);
        var headers = producer.Produced[0].Message.Headers;
        Assert.NotNull(headers);
        Assert.Equal("true", Encoding.UTF8.GetString(headers.GetLastBytes("is_dummy")));
    }
}
