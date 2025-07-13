using Confluent.Kafka;
using Kafka.Ksql.Linq.Messaging.Abstractions;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Kafka.Ksql.Linq.Messaging.Consumers;

/// <summary>
/// Simple byte-based Kafka consumer.
/// </summary>
public sealed class RawKafkaConsumer : IRawKafkaConsumer, IDisposable
{
    private readonly IConsumer<byte[], byte[]> _consumer;

    public RawKafkaConsumer(IConsumer<byte[], byte[]> consumer)
    {
        _consumer = consumer ?? throw new ArgumentNullException(nameof(consumer));
    }

    public Task<(byte[]? keyBytes, byte[]? valueBytes)> ConsumeAsync(string topic, CancellationToken cancellationToken = default)
    {
        _consumer.Subscribe(topic);
        var result = _consumer.Consume(cancellationToken);
        return Task.FromResult((result.Message.Key, result.Message.Value));
    }

    public void Dispose() => _consumer.Dispose();
}
