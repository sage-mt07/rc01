using System;
using System.Reflection;
using Kafka.Ksql.Linq.Serialization.Avro.Management;
using Xunit;
using static Kafka.Ksql.Linq.Tests.PrivateAccessor;

namespace Kafka.Ksql.Linq.Serialization.Tests;

public class AvroSchemaBuilderTests
{
    private class Sample
    {
        public bool Flag { get; set; }
        public object Any { get; set; } = new();
    }

    [Fact]
    public void GetBasicAvroType_PrimitiveAndFallback()
    {
        var builder = new AvroSchemaBuilder();
        var flag = typeof(Sample).GetProperty(nameof(Sample.Flag))!;
        var obj = InvokePrivate<object>(builder, "GetBasicAvroType", new[] { typeof(PropertyInfo), typeof(Type) }, null, flag, typeof(bool));
        Assert.Equal("boolean", obj);

        var any = typeof(Sample).GetProperty(nameof(Sample.Any))!;
        obj = InvokePrivate<object>(builder, "GetBasicAvroType", new[] { typeof(PropertyInfo), typeof(Type) }, null, any, typeof(object));
        Assert.Equal("string", obj);
    }
}
