namespace Kafka.Ksql.Linq.Messaging.Abstractions;

using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Byte-oriented Kafka producer. Sends raw key/value bytes to a topic.
/// </summary>
public interface IRawKafkaProducer
{
    /// <summary>
    /// Publish key and value bytes to the specified topic.
    /// </summary>
    Task PublishAsync(byte[]? keyBytes, byte[]? valueBytes, string topic, CancellationToken cancellationToken = default);
}
