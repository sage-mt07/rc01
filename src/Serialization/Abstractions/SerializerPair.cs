using System;
using Confluent.Kafka;

namespace Kafka.Ksql.Linq.Serialization.Abstractions;

public class SerializerPair<T> where T : class
{
    public ISerializer<object> KeySerializer { get; set; } = default!;
    public ISerializer<object> ValueSerializer { get; set; } = default!;
    public int KeySchemaId { get; set; }
    public int ValueSchemaId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
