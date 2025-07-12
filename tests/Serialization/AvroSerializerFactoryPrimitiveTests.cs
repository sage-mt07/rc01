using System;
using System.Reflection;
using Confluent.SchemaRegistry;
using Confluent.Kafka;
using Kafka.Ksql.Linq.Serialization.Avro.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static Kafka.Ksql.Linq.Tests.PrivateAccessor;

namespace Kafka.Ksql.Linq.Tests.Serialization;

public class AvroSerializerFactoryPrimitiveTests
{
    private static AvroSerializerFactory CreateFactory()
    {
        var client = DispatchProxy.Create<ISchemaRegistryClient, FakeSchemaRegistryClient>();
        return new AvroSerializerFactory(client, new NullLoggerFactory());
    }

    [Theory]
    [InlineData(typeof(string), typeof(StringKeySerializer))]
    [InlineData(typeof(int), typeof(IntKeySerializer))]
    [InlineData(typeof(long), typeof(LongKeySerializer))]
    [InlineData(typeof(Guid), typeof(GuidKeySerializer))]
    public void CreatePrimitiveKeySerializer_ReturnsExpected(Type type, Type expected)
    {
        var factory = CreateFactory();
        var ser = InvokePrivate<ISerializer<object>>(factory, "CreatePrimitiveKeySerializer", new[] { typeof(Type) }, null, type);
        Assert.IsType(expected, ser);
    }

    [Fact]
    public void CreatePrimitiveKeySerializer_Unsupported_Throws()
    {
        var factory = CreateFactory();
        Assert.Throws<NotSupportedException>(() =>
            InvokePrivate<ISerializer<object>>(factory, "CreatePrimitiveKeySerializer", new[] { typeof(Type) }, null, typeof(DateTime)));
    }

    [Theory]
    [InlineData(typeof(string), typeof(StringKeyDeserializer))]
    [InlineData(typeof(int), typeof(IntKeyDeserializer))]
    [InlineData(typeof(long), typeof(LongKeyDeserializer))]
    [InlineData(typeof(Guid), typeof(GuidKeyDeserializer))]
    public void CreatePrimitiveKeyDeserializer_ReturnsExpected(Type type, Type expected)
    {
        var factory = CreateFactory();
        var des = InvokePrivate<IDeserializer<object>>(factory, "CreatePrimitiveKeyDeserializer", new[] { typeof(Type) }, null, type);
        Assert.IsType(expected, des);
    }

    [Fact]
    public void CreatePrimitiveKeyDeserializer_Unsupported_Throws()
    {
        var factory = CreateFactory();
        Assert.Throws<NotSupportedException>(() =>
            InvokePrivate<IDeserializer<object>>(factory, "CreatePrimitiveKeyDeserializer", new[] { typeof(Type) }, null, typeof(DateTime)));
    }
}
