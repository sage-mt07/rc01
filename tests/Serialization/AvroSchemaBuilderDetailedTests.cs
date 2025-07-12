using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Serialization.Avro.Core;
using Kafka.Ksql.Linq.Serialization.Avro.Management;
using Xunit;
using static Kafka.Ksql.Linq.Tests.PrivateAccessor;

#nullable enable

namespace Kafka.Ksql.Linq.Tests.Serialization;

public class AvroSchemaBuilderDetailedTests
{
    private class SampleEntity
    {
        public int Id { get; set; }
        public Guid GuidKey { get; set; }
        public string? Name { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Ignore { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public DateTime Date { get; set; }
    }

    private class NoTopic
    {
        public int Id { get; set; }
    }

    [Fact]
    public void GenerateCompositeKeySchema_BuildsRecord()
    {
        var builder = new AvroSchemaBuilder();
        var props = new[]
        {
            typeof(SampleEntity).GetProperty(nameof(SampleEntity.Id))!,
            typeof(SampleEntity).GetProperty(nameof(SampleEntity.GuidKey))!
        };
        var json = InvokePrivate<string>(builder, "GenerateCompositeKeySchema", new[] { typeof(PropertyInfo[]) }, null, (object)props);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("record", doc.RootElement.GetProperty("type").GetString());
        Assert.Equal(2, doc.RootElement.GetProperty("fields").GetArrayLength());
    }

    [Fact]
    public void GenerateFieldsFromProperties_ReturnsAllFields()
    {
        var builder = new AvroSchemaBuilder();
        var props = InvokePrivate<PropertyInfo[]>(builder, "GetSchemaProperties", new[] { typeof(Type) }, null, typeof(SampleEntity));
        var fields = InvokePrivate<List<AvroField>>(builder, "GenerateFieldsFromProperties", new[] { typeof(PropertyInfo[]) }, null, (object)props);
        Assert.Contains(fields, f => f.Name == nameof(SampleEntity.Ignore));
        Assert.Contains(fields, f => f.Name == nameof(SampleEntity.Name));
    }

    [Fact]
    public void GenerateValueSchema_CreatesRecord()
    {
        var builder = new AvroSchemaBuilder();
        var json = builder.GenerateValueSchema<SampleEntity>();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("record", doc.RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public void GetBasicAvroType_ReturnsLogicalTypeForSpecials()
    {
        var builder = new AvroSchemaBuilder();
        var price = typeof(SampleEntity).GetProperty(nameof(SampleEntity.Price))!;
        var obj = InvokePrivate<object>(builder, "GetBasicAvroType", new[] { typeof(PropertyInfo), typeof(Type) }, null, price, typeof(decimal));
        var json = JsonSerializer.Serialize(obj);
        Assert.Contains("logicalType", json);
        var date = typeof(SampleEntity).GetProperty(nameof(SampleEntity.Date))!;
        obj = InvokePrivate<object>(builder, "GetBasicAvroType", new[] { typeof(PropertyInfo), typeof(Type) }, null, date, typeof(DateTime));
        json = JsonSerializer.Serialize(obj);
        Assert.Contains("timestamp-millis", json);
        var guid = typeof(SampleEntity).GetProperty(nameof(SampleEntity.GuidKey))!;
        obj = InvokePrivate<object>(builder, "GetBasicAvroType", new[] { typeof(PropertyInfo), typeof(Type) }, null, guid, typeof(Guid));
        json = JsonSerializer.Serialize(obj);
        Assert.Contains("uuid", json);
    }

    [Fact]
    public async Task GetKeySchemaAsync_ReturnsSchema()
    {
        var builder = new AvroSchemaBuilder();
        var schema = await builder.GetKeySchemaAsync<SampleEntity>();
        Assert.Equal("\"string\"", schema);
    }

    [Fact]
    public void GetSchemaProperties_ReturnsAllProperties()
    {
        var builder = new AvroSchemaBuilder();
        var props = InvokePrivate<PropertyInfo[]>(builder, "GetSchemaProperties", new[] { typeof(Type) }, null, typeof(SampleEntity));
        Assert.Contains(props, p => p.Name == nameof(SampleEntity.Ignore));
        Assert.Contains(props, p => p.Name == nameof(SampleEntity.Name));
    }

    [Fact]
    public async Task GetSchemasAsync_ReturnsKeyAndValue()
    {
        var builder = new AvroSchemaBuilder();
        var pair = await builder.GetSchemasAsync<SampleEntity>();
        Assert.Equal("\"string\"", pair.keySchema);
        Assert.Contains("SampleEntity", pair.valueSchema);
    }

    [Fact]
    public void GetTopicName_ReturnsAttributeValue()
    {
        var builder = new AvroSchemaBuilder();
        var name = InvokePrivate<string>(builder, "GetTopicName", new[] { typeof(Type) }, null, typeof(SampleEntity));
        Assert.Equal("sampleentity", name);
        name = InvokePrivate<string>(builder, "GetTopicName", new[] { typeof(Type) }, null, typeof(NoTopic));
        Assert.Equal("notopic", name);
    }

    [Fact]
    public async Task GetValueSchemaAsync_ReturnsValueSchema()
    {
        var builder = new AvroSchemaBuilder();
        var val = await builder.GetValueSchemaAsync<SampleEntity>();
        Assert.Contains("SampleEntity", val);
    }

    [Fact]
    public void IsNullableReferenceType_DetectsCorrectly()
    {
        var builder = new AvroSchemaBuilder();
        var nullableProp = typeof(SampleEntity).GetProperty(nameof(SampleEntity.Name))!;
        var nonNullableProp = typeof(SampleEntity).GetProperty(nameof(SampleEntity.Description))!;
        Assert.True(InvokePrivate<bool>(builder, "IsNullableReferenceType", new[] { typeof(PropertyInfo) }, null, nullableProp));
        Assert.False(InvokePrivate<bool>(builder, "IsNullableReferenceType", new[] { typeof(PropertyInfo) }, null, nonNullableProp));
    }

    [Fact]
    public void MapPropertyToAvroType_ReturnsNullableArrayForReference()
    {
        var builder = new AvroSchemaBuilder();
        var prop = typeof(SampleEntity).GetProperty(nameof(SampleEntity.Name))!;
        var result = InvokePrivate<object>(builder, "MapPropertyToAvroType", new[] { typeof(PropertyInfo) }, null, prop);
        Assert.IsType<object[]>(result);
    }

    [Fact]
    public void SerializeSchema_UsesCamelCase()
    {
        var builder = new AvroSchemaBuilder();
        var schema = new AvroSchema { Type = "record", Name = "Test" };
        var json = InvokePrivate<string>(builder, "SerializeSchema", new[] { typeof(AvroSchema) }, null, schema);
        Assert.Contains("name", json);
    }

    [Fact]
    public void ValidateAvroSchema_ReturnsTrueForValidRecord()
    {
        var builder = new AvroSchemaBuilder();
        var schema = "{ \"type\": \"record\", \"name\": \"T\" }";
        var valid = InvokePrivate<bool>(builder, "ValidateAvroSchema", new[] { typeof(string) }, null, schema);
        Assert.True(valid);
    }

    [Fact]
    public void ValidateAvroSchema_ReturnsFalseForInvalidRecord()
    {
        var builder = new AvroSchemaBuilder();
        var schema = "{ \"type\": \"record\" }"; // missing name
        var valid = InvokePrivate<bool>(builder, "ValidateAvroSchema", new[] { typeof(string) }, null, schema);
        Assert.False(valid);
    }

    [Fact]
    public void ValidateAvroSchema_ReturnsFalseForEmpty()
    {
        var builder = new AvroSchemaBuilder();
        var valid = InvokePrivate<bool>(builder, "ValidateAvroSchema", new[] { typeof(string) }, null, "");
        Assert.False(valid);
    }

    [Fact]
    public void ValidateAvroSchema_ReturnsFalseForInvalidJson()
    {
        var builder = new AvroSchemaBuilder();
        var valid = InvokePrivate<bool>(builder, "ValidateAvroSchema", new[] { typeof(string) }, null, "{");
        Assert.False(valid);
    }

    [Fact]
    public void ValidateAvroSchema_ReturnsTrueForPrimitiveString()
    {
        var builder = new AvroSchemaBuilder();
        var valid = InvokePrivate<bool>(builder, "ValidateAvroSchema", new[] { typeof(string) }, null, "\"int\"");
        Assert.True(valid);
    }

    [Fact]
    public void ValidateAvroSchema_ReturnsTrueForArray()
    {
        var builder = new AvroSchemaBuilder();
        var valid = InvokePrivate<bool>(builder, "ValidateAvroSchema", new[] { typeof(string) }, null, "[1]");
        Assert.True(valid);
    }

    [Theory]
    [InlineData(typeof(decimal), "decimal")]
    [InlineData(typeof(DateTime), "timestamp-millis")]
    [InlineData(typeof(Guid), "uuid")]
    [InlineData(typeof(string), "string")]
    public void GeneratePrimitiveSchema_ReturnsExpected(Type type, string expected)
    {
        var builder = new AvroSchemaBuilder();
        var json = InvokePrivate<string>(builder, "GeneratePrimitiveSchema", new[] { typeof(Type) }, null, type);
        Assert.Contains(expected, json);
    }
}
