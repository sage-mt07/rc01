using System;
using Confluent.Kafka;
using Kafka.Ksql.Linq.Serialization.Abstractions;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Serialization;

public class SerializerAbstractionsTests
{
    private class DummySerializer : ISerializer<object>
    {
        public byte[] Serialize(object data, SerializationContext context) => Array.Empty<byte>();
    }

    private class DummyDeserializer : IDeserializer<object>
    {
        public object Deserialize(ReadOnlySpan<byte> data, bool isNull, SerializationContext context) => new object();
    }

    [Fact]
    public void SerializerPair_PropertyAccessors()
    {
        var keySerializer = new DummySerializer();
        var valueSerializer = new DummySerializer();
        var pair = new SerializerPair<string>
        {
            KeySerializer = keySerializer,
            ValueSerializer = valueSerializer,
            KeySchemaId = 1,
            ValueSchemaId = 2
        };

        Assert.Same(keySerializer, pair.KeySerializer);
        Assert.Same(valueSerializer, pair.ValueSerializer);
        Assert.Equal(1, pair.KeySchemaId);
        Assert.Equal(2, pair.ValueSchemaId);
        Assert.InRange(DateTime.UtcNow - pair.CreatedAt, TimeSpan.Zero, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void DeserializerPair_PropertyAccessors()
    {
        var keyDeserializer = new DummyDeserializer();
        var valueDeserializer = new DummyDeserializer();
        var pair = new DeserializerPair<string>
        {
            KeyDeserializer = keyDeserializer,
            ValueDeserializer = valueDeserializer,
            KeySchemaId = 3,
            ValueSchemaId = 4
        };

        Assert.Same(keyDeserializer, pair.KeyDeserializer);
        Assert.Same(valueDeserializer, pair.ValueDeserializer);
        Assert.Equal(3, pair.KeySchemaId);
        Assert.Equal(4, pair.ValueSchemaId);
        Assert.InRange(DateTime.UtcNow - pair.CreatedAt, TimeSpan.Zero, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void SerializationStatistics_PropertyAccessors()
    {
        var stats = new SerializationStatistics
        {
            AverageLatency = TimeSpan.FromMilliseconds(50),
            LastUpdated = new DateTime(2024, 1, 1)
        };
        stats.CacheHits = 5;
        stats.TotalSerializations = 10;

        Assert.Equal(TimeSpan.FromMilliseconds(50), stats.AverageLatency);
        Assert.Equal(new DateTime(2024, 1, 1), stats.LastUpdated);
        Assert.Equal(0.5, stats.HitRate, 3);
    }
}
