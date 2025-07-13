namespace Kafka.Ksql.Linq.Serialization.Abstractions;

using Confluent.Kafka;

/// <summary>
/// Provides typed serializers for Kafka messages.
/// </summary>
public interface ISerializerFactory
{
    /// <summary>
    /// Create a serializer for the given type.
    /// </summary>
    ISerializer<T> CreateSerializer<T>();
}
