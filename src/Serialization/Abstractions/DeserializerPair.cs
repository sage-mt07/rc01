using System;
using Confluent.Kafka;

namespace Kafka.Ksql.Linq.Serialization.Abstractions;

public class DeserializerPair<T> where T : class
{
    public IDeserializer<object> KeyDeserializer { get; set; } = default!;
    public IDeserializer<object> ValueDeserializer { get; set; } = default!;
    public int KeySchemaId { get; set; }
    public int ValueSchemaId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
