namespace Kafka.Ksql.Linq.Messaging.Abstractions;

using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Byte-oriented Kafka consumer. Consumes raw key/value bytes from a topic.
/// </summary>
public interface IRawKafkaConsumer
{
    /// <summary>
    /// Consume a single record from the specified topic.
    /// </summary>
    Task<(byte[]? keyBytes, byte[]? valueBytes)> ConsumeAsync(string topic, CancellationToken cancellationToken = default);
}
