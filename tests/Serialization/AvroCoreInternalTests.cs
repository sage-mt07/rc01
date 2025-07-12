using System;
using System.Reflection;
using Confluent.SchemaRegistry;
using Confluent.Kafka;
using Kafka.Ksql.Linq.Serialization.Avro.Core;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Serialization;

public class AvroCoreInternalTests
{
    [Fact]
    public void AvroField_PropertyAccessors()
    {
        var field = new AvroField
        {
            Name = "id",
            Type = "int",
            Doc = "doc",
            Default = 0
        };

        Assert.Equal("id", field.Name);
        Assert.Equal("int", field.Type);
        Assert.Equal("doc", field.Doc);
        Assert.Equal(0, field.Default);
    }

    [Fact]
    public void AvroSchema_PropertyAccessors()
    {
        var avroField = new AvroField { Name = "id", Type = "int" };
        var schema = new AvroSchema
        {
            Type = "record",
            Name = "Sample",
            Namespace = "ns",
            Doc = "doc",
            Fields = new() { avroField }
        };

        Assert.Equal("record", schema.Type);
        Assert.Equal("Sample", schema.Name);
        Assert.Equal("ns", schema.Namespace);
        Assert.Equal("doc", schema.Doc);
        Assert.Single(schema.Fields);
        Assert.Same(avroField, schema.Fields[0]);
    }

    [Fact]
    public void AvroSchemaInfo_RegisteredAt_Getter()
    {
        var date = new DateTime(2022, 1, 1);
        var info = new AvroSchemaInfo { RegisteredAt = date };
        Assert.Equal(date, info.RegisteredAt);
    }

    private class Sample { public int Id { get; set; } }

    [Fact]
    public void AvroValueSerializer_And_Deserializer_RoundTrip()
    {
        var client = DispatchProxy.Create<ISchemaRegistryClient, FakeSchemaRegistryClient>();
        var serializer = new AvroValueSerializer<Sample>(client);
        var deserializer = new AvroValueDeserializer<Sample>(client);

        var field = typeof(AvroValueSerializer<Sample>).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.Same(client, field!.GetValue(serializer));
        field = typeof(AvroValueDeserializer<Sample>).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.Same(client, field!.GetValue(deserializer));

        var context = new SerializationContext(MessageComponentType.Value, "t");
        var obj = new Sample { Id = 42 };
        var bytes = serializer.Serialize(obj, context);
        var result = (Sample)deserializer.Deserialize(bytes, false, context);
        Assert.Equal(obj.Id, result.Id);
    }

    [Fact]
    public void GuidKeySerializer_And_Deserializer()
    {
        var serializer = new GuidKeySerializer();
        var deserializer = new GuidKeyDeserializer();
        var guid = Guid.NewGuid();
        var context = new SerializationContext();
        var bytes = serializer.Serialize(guid, context);
        var result = deserializer.Deserialize(bytes, false, context);
        Assert.Equal(guid, result);
        Assert.Equal(Guid.Empty, deserializer.Deserialize(ReadOnlySpan<byte>.Empty, true, context));
    }

    [Fact]
    public void IntKeySerializer_And_Deserializer()
    {
        var serializer = new IntKeySerializer();
        var deserializer = new IntKeyDeserializer();
        var number = 123;
        var context = new SerializationContext();
        var bytes = serializer.Serialize(number, context);
        var result = deserializer.Deserialize(bytes, false, context);
        Assert.Equal(number, result);
        Assert.Equal(0, deserializer.Deserialize(ReadOnlySpan<byte>.Empty, true, context));
    }

    [Fact]
    public void LongKeySerializer_And_Deserializer()
    {
        var serializer = new LongKeySerializer();
        var deserializer = new LongKeyDeserializer();
        long number = 9876543210L;
        var context = new SerializationContext();
        var bytes = serializer.Serialize(number, context);
        var result = deserializer.Deserialize(bytes, false, context);
        Assert.Equal(number, result);
        Assert.Equal(0L, deserializer.Deserialize(ReadOnlySpan<byte>.Empty, true, context));
    }
}
