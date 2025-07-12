using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Kafka.Ksql.Linq.Configuration.Abstractions;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Serialization.Avro.Core;
using Kafka.Ksql.Linq.Serialization.Abstractions;
using static Kafka.Ksql.Linq.Tests.PrivateAccessor;

#nullable enable
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Serialization;

public class UnifiedSchemaGeneratorTests
{
    private class SampleEntity
    {
        public int Id { get; set; }
        public Guid GuidKey { get; set; }
        public string? Name { get; set; }
        public int? OptionalNumber { get; set; }
        public decimal Price { get; set; }
        public DateTime Date { get; set; }
        public string Ignore { get; set; } = string.Empty;
    }


    [Fact]
    public void ToPascalCase_Works_WithVariousDelimiters()
    {
        var result = InvokePrivate<string>(typeof(UnifiedSchemaGenerator), "ToPascalCase", new[] { typeof(string) }, null, "sample_topic-name.text");
        Assert.Equal("SampleTopicNameText", result);
        Assert.Equal(string.Empty, InvokePrivate<string>(typeof(UnifiedSchemaGenerator), "ToPascalCase", new[] { typeof(string) }, null, ""));
    }

    [Fact]
    public void IsPrimitiveType_KnownTypes()
    {
        Assert.True(InvokePrivate<bool>(typeof(UnifiedSchemaGenerator), "IsPrimitiveType", new[] { typeof(Type) }, null, typeof(string)));
        Assert.False(InvokePrivate<bool>(typeof(UnifiedSchemaGenerator), "IsPrimitiveType", new[] { typeof(Type) }, null, typeof(DateTime)));
    }

    [Fact]
    public void IsNullableProperty_DetectsNullableCorrectly()
    {
        var props = typeof(SampleEntity).GetProperties();
        Assert.False(InvokePrivate<bool>(typeof(UnifiedSchemaGenerator), "IsNullableProperty", new[] { typeof(PropertyInfo) }, null, props.First(p => p.Name == nameof(SampleEntity.Id))));
        Assert.True(InvokePrivate<bool>(typeof(UnifiedSchemaGenerator), "IsNullableProperty", new[] { typeof(PropertyInfo) }, null, props.First(p => p.Name == nameof(SampleEntity.OptionalNumber))));
        Assert.True(InvokePrivate<bool>(typeof(UnifiedSchemaGenerator), "IsNullableProperty", new[] { typeof(PropertyInfo) }, null, props.First(p => p.Name == nameof(SampleEntity.Name))));
    }

    [Fact]
    public void GetSerializableProperties_ReturnsAllProperties()
    {
        var props = InvokePrivate<PropertyInfo[]>(typeof(UnifiedSchemaGenerator), "GetSerializableProperties", new[] { typeof(Type) }, null, typeof(SampleEntity));
        Assert.Contains(props, p => p.Name == nameof(SampleEntity.Ignore));
        Assert.Contains(props, p => p.Name == nameof(SampleEntity.Name));
    }

    [Fact]
    public void GetIgnoredProperties_ReturnsEmpty()
    {
        var props = InvokePrivate<PropertyInfo[]>(typeof(UnifiedSchemaGenerator), "GetIgnoredProperties", new[] { typeof(Type) }, null, typeof(SampleEntity));
        Assert.Empty(props);
    }

    [Fact]
    public void MapPropertyToAvroType_HandlesNullable()
    {
        var prop = typeof(SampleEntity).GetProperty(nameof(SampleEntity.Name))!;
        var result = InvokePrivate<object>(typeof(UnifiedSchemaGenerator), "MapPropertyToAvroType", new[] { typeof(PropertyInfo) }, null, prop);
        Assert.IsType<object[]>(result);
    }

    [Fact]
    public void GetAvroType_MapsSpecialTypes()
    {
        var price = typeof(SampleEntity).GetProperty(nameof(SampleEntity.Price))!;
        var avro = InvokePrivate<object>(typeof(UnifiedSchemaGenerator), "GetAvroType", new[] { typeof(PropertyInfo) }, null, price);
        var json = JsonSerializer.Serialize(avro);
        Assert.Contains("logicalType", json);
        var dtProp = typeof(SampleEntity).GetProperty(nameof(SampleEntity.Date))!;
        avro = InvokePrivate<object>(typeof(UnifiedSchemaGenerator), "GetAvroType", new[] { typeof(PropertyInfo) }, null, dtProp);
        json = JsonSerializer.Serialize(avro);
        Assert.Contains("timestamp-millis", json);
        var guidProp = typeof(SampleEntity).GetProperty(nameof(SampleEntity.GuidKey))!;
        avro = InvokePrivate<object>(typeof(UnifiedSchemaGenerator), "GetAvroType", new[] { typeof(PropertyInfo) }, null, guidProp);
        json = JsonSerializer.Serialize(avro);
        Assert.Contains("uuid", json);
    }

    [Fact]
    public void GeneratePrimitiveKeySchema_PrimitiveTypes()
    {
        var schema = InvokePrivate<string>(typeof(UnifiedSchemaGenerator), "GeneratePrimitiveKeySchema", new[] { typeof(Type) }, null, typeof(int));
        Assert.Equal("\"int\"", schema);
        schema = InvokePrivate<string>(typeof(UnifiedSchemaGenerator), "GeneratePrimitiveKeySchema", new[] { typeof(Type) }, null, typeof(Guid));
        Assert.Contains("uuid", schema);
    }

    [Fact]
    public void GenerateNullablePrimitiveKeySchema_PrimitiveTypes()
    {
        var schema = InvokePrivate<string>(typeof(UnifiedSchemaGenerator), "GenerateNullablePrimitiveKeySchema", new[] { typeof(Type) }, null, typeof(int));
        Assert.Contains("null", schema);
        schema = InvokePrivate<string>(typeof(UnifiedSchemaGenerator), "GenerateNullablePrimitiveKeySchema", new[] { typeof(Type) }, null, typeof(Guid));
        Assert.Contains("uuid", schema);
    }

    [Fact]
    public void GenerateCompositeKeySchema_BuildsRecord()
    {
        var keys = new[]
        {
            typeof(SampleEntity).GetProperty(nameof(SampleEntity.Id))!,
            typeof(SampleEntity).GetProperty(nameof(SampleEntity.GuidKey))!
        };
        var json = InvokePrivate<string>(typeof(UnifiedSchemaGenerator), "GenerateCompositeKeySchema", new[] { typeof(PropertyInfo[]) }, null, (object)keys);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal("record", root.GetProperty("type").GetString(), ignoreCase: true);
        Assert.Equal("CompositeKey", root.GetProperty("name").GetString(), ignoreCase: true);

        var fieldNames = root.GetProperty("fields")
            .EnumerateArray()
            .Select(f => f.GetProperty("name").GetString())
            .ToArray();

        Assert.Contains(fieldNames, f => string.Equals(f, "id", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(fieldNames, f => string.Equals(f, "guidKey", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GenerateFields_CreatesFields()
    {
        var fields = InvokePrivate<List<AvroField>>(typeof(UnifiedSchemaGenerator), "GenerateFields", new[] { typeof(Type) }, null, typeof(SampleEntity));
        Assert.Contains(fields, f => f.Name == nameof(SampleEntity.Name));
        Assert.Contains(fields, f => f.Name == nameof(SampleEntity.Ignore));
    }

    [Fact]
    public void GenerateFieldsFromConfiguration_UsesConfiguration()
    {
        var cfg = new AvroEntityConfiguration(typeof(SampleEntity));
        var fields = InvokePrivate<List<AvroField>>(typeof(UnifiedSchemaGenerator), "GenerateFieldsFromConfiguration", new[] { typeof(AvroEntityConfiguration) }, null, cfg);
        Assert.Contains(fields, f => f.Name == nameof(SampleEntity.Id));
    }

    [Fact]
    public void SerializeSchema_WorksWithOptions()
    {
        var schema = new AvroSchema { Type = "record", Name = "R" };
        var json = InvokePrivate<string>(typeof(UnifiedSchemaGenerator), "SerializeSchema", new[] { typeof(AvroSchema) }, null, schema);

        using (var doc = JsonDocument.Parse(json))
        {
            Assert.Equal("record", doc.RootElement.GetProperty("type").GetString(), ignoreCase: true);
        }

        var opts = new SchemaGenerationOptions { PrettyFormat = false, UseKebabCase = true };
        json = InvokePrivate<string>(typeof(UnifiedSchemaGenerator), "SerializeSchema", new[] { typeof(AvroSchema), typeof(SchemaGenerationOptions) }, null, schema, opts);

        using (var doc2 = JsonDocument.Parse(json))
        {
            Assert.True(doc2.RootElement.TryGetProperty("type", out var _));
        }
    }

    [Fact]
    public void GenerateSchema_GeneratesValidSchema()
    {
        var json = UnifiedSchemaGenerator.GenerateSchema(typeof(SampleEntity));
        Assert.True(UnifiedSchemaGenerator.ValidateSchema(json));
    }

    [Fact]
    public void GenerateKeySchema_PrimitiveAndComplex()
    {
        var primitive = UnifiedSchemaGenerator.GenerateKeySchema(typeof(int));
        Assert.Equal("\"int\"", primitive);
        var complex = UnifiedSchemaGenerator.GenerateKeySchema(typeof(SampleEntity));
        Assert.Contains("record", complex);
    }

    [Fact]
    public void GenerateKeySchema_FromConfiguration()
    {
        var cfg = new AvroEntityConfiguration(typeof(SampleEntity))
        {
            KeyProperties = new[]
            {
                typeof(SampleEntity).GetProperty(nameof(SampleEntity.Id))!,
                typeof(SampleEntity).GetProperty(nameof(SampleEntity.GuidKey))!
            }
        };
        var json = UnifiedSchemaGenerator.GenerateKeySchema(cfg);
        Assert.Contains("CompositeKey", json);
    }

    [Fact]
    public void GenerateKeySchema_UsePropertyOrderKeys_MaintainsDefinitionOrder()
    {
        var cfg = new AvroEntityConfiguration(typeof(SampleEntity));
        var builder = new AvroEntityConfigurationBuilder<SampleEntity>(cfg);
        builder.UsePropertyOrderKeys(2);
        var json = UnifiedSchemaGenerator.GenerateKeySchema(cfg);
        using var doc = JsonDocument.Parse(json);
        var fields = doc.RootElement.GetProperty("fields")
            .EnumerateArray()
            .Select(f => f.GetProperty("name").GetString())
            .ToArray();
        Assert.Equal(new[] { nameof(SampleEntity.Id), nameof(SampleEntity.GuidKey) }, fields);
    }

    [Fact]
    public void GenerateValueSchema_UsesEntityType()
    {
        var json = UnifiedSchemaGenerator.GenerateValueSchema(typeof(SampleEntity));
        Assert.Contains("record", json);
    }

    [Fact]
    public void GenerateTopicSchemas_ReturnsPair()
    {
        var pair = UnifiedSchemaGenerator.GenerateTopicSchemas<int, SampleEntity>();
        Assert.True(UnifiedSchemaGenerator.ValidateSchema(pair.keySchema));
        Assert.True(UnifiedSchemaGenerator.ValidateSchema(pair.valueSchema));
    }

    [Fact]
    public void GenerateTopicSchemas_WithName_UsesCustomName()
    {
        var pair = UnifiedSchemaGenerator.GenerateTopicSchemas<int, SampleEntity>("custom-topic");
        Assert.Contains("Custom-topic_value", UnifiedSchemaGenerator.GenerateSchema(typeof(SampleEntity), new SchemaGenerationOptions { CustomName = "Custom-topic_value" }));
        Assert.True(UnifiedSchemaGenerator.ValidateSchema(pair.valueSchema));
    }

    [Fact]
    public void GetGenerationStats_ReturnsCounts()
    {
        var stats = UnifiedSchemaGenerator.GetGenerationStats(typeof(SampleEntity));
        Assert.True(stats.TotalProperties >= 1);
        Assert.Empty(stats.IgnoredPropertyNames);
    }

    [Fact]
    public void ValidateSchema_WorksForVariousInputs()
    {
        Assert.False(UnifiedSchemaGenerator.ValidateSchema(""));
        Assert.True(UnifiedSchemaGenerator.ValidateSchema("\"string\""));
        Assert.False(UnifiedSchemaGenerator.ValidateSchema("{ invalid }"));
    }
}

