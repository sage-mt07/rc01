namespace Kafka.Ksql.Linq.Serialization.Abstractions;

using Confluent.Kafka;

/// <summary>
/// Provides typed deserializers for Kafka messages.
/// </summary>
public interface IDeserializerFactory
{
    /// <summary>
    /// Create a deserializer for the given type.
    /// </summary>
    IDeserializer<T> CreateDeserializer<T>();
}
