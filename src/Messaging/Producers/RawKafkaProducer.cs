using Confluent.Kafka;
using Kafka.Ksql.Linq.Messaging.Abstractions;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Kafka.Ksql.Linq.Messaging.Producers;

/// <summary>
/// Simple byte-based Kafka producer.
/// </summary>
public sealed class RawKafkaProducer : IRawKafkaProducer, IDisposable
{
    private readonly IProducer<byte[], byte[]> _producer;

    public RawKafkaProducer(IProducer<byte[], byte[]> producer)
    {
        _producer = producer ?? throw new ArgumentNullException(nameof(producer));
    }

    public async Task PublishAsync(byte[]? keyBytes, byte[]? valueBytes, string topic, CancellationToken cancellationToken = default)
    {
        var message = new Message<byte[], byte[]> { Key = keyBytes!, Value = valueBytes! };
        await _producer.ProduceAsync(topic, message, cancellationToken);
    }

    public void Dispose() => _producer.Dispose();
}
