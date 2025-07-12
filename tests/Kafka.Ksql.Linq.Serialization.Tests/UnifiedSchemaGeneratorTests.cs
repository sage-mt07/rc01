using System.Reflection;
using Kafka.Ksql.Linq.Serialization.Avro.Core;
using Xunit;
using static Kafka.Ksql.Linq.Tests.PrivateAccessor;

namespace Kafka.Ksql.Linq.Serialization.Tests;

public class UnifiedSchemaGeneratorTests
{
    private class Sample
    {
        public bool Flag { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    [Fact]
    public void GetAvroType_PrimitiveTypes_ReturnExpected()
    {
        var boolProp = typeof(Sample).GetProperty(nameof(Sample.Flag))!;
        var result = InvokePrivate<object>(typeof(UnifiedSchemaGenerator), "GetAvroType", new[] { typeof(PropertyInfo) }, null, boolProp);
        Assert.Equal("boolean", result);

        var stringProp = typeof(Sample).GetProperty(nameof(Sample.Name))!;
        result = InvokePrivate<object>(typeof(UnifiedSchemaGenerator), "GetAvroType", new[] { typeof(PropertyInfo) }, null, stringProp);
        Assert.Equal("string", result);
    }
}
