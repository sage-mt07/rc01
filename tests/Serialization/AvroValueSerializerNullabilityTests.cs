using System;
using Confluent.SchemaRegistry;
using System.Reflection;
using Confluent.Kafka;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Serialization.Avro.Core;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Serialization;

public class AvroValueSerializerNullabilityTests
{
    private class NullableEntity
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    [Fact]
    public void NullableProperty_RoundTrips()
    {
        var client = DispatchProxy.Create<ISchemaRegistryClient, FakeSchemaRegistryClient>();
        var serializer = new AvroValueSerializer<NullableEntity>(client);
        var deserializer = new AvroValueDeserializer<NullableEntity>(client);
        var entity = new NullableEntity { Id = 1, Name = null };
        var ctx = new SerializationContext(MessageComponentType.Value, "nulltopic");
        var bytes = serializer.Serialize(entity, ctx);
        var result = (NullableEntity)deserializer.Deserialize(bytes, false, ctx);
        Assert.Equal(entity.Id, result.Id);
        Assert.Null(result.Name);
    }

    [Fact]
    public void Serialize_WrongType_Throws()
    {
        var client = DispatchProxy.Create<ISchemaRegistryClient, FakeSchemaRegistryClient>();
        var serializer = new AvroValueSerializer<NullableEntity>(client);
        var ctx = new SerializationContext(MessageComponentType.Value, "nulltopic");
        Assert.Throws<InvalidOperationException>(() => serializer.Serialize("abc", ctx));
    }

    [Fact]
    public void Deserialize_InvalidJson_Throws()
    {
        var client = DispatchProxy.Create<ISchemaRegistryClient, FakeSchemaRegistryClient>();
        var deserializer = new AvroValueDeserializer<NullableEntity>(client);
        var ctx = new SerializationContext(MessageComponentType.Value, "nulltopic");
        var bytes = System.Text.Encoding.UTF8.GetBytes("{\"Id\":\"a\"}");
        Assert.ThrowsAny<Exception>(() => deserializer.Deserialize(bytes, false, ctx));
    }
}
