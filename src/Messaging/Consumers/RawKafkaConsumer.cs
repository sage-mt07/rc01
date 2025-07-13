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

    public async Task<(byte[]? keyBytes, byte[]? valueBytes)> ConsumeAsync(string topic, CancellationToken cancellationToken = default)
    {
        var result = _consumer.Consume(cancellationToken);
        await Task.Delay(1, cancellationToken);
        return (result.Message.Key, result.Message.Value);
    }

    public async Task CommitAsync()
    {
        _consumer.Commit();
        await Task.Delay(1);
    }

    public void Dispose() => _consumer.Dispose();
}
