using Kafka.Ksql.Linq.Mapping;
using Kafka.Ksql.Linq.Query.Schema;
using Kafka.Ksql.Linq.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Mapping;

public class PocoMapperTests
{
    private class Sample
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private class CompositeSample
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
    }

    private class UnsupportedSample
    {
        public decimal Id { get; set; }
    }

    private class NullComposite
    {
        public int Id { get; set; }
        public string? Code { get; set; }
    }

    private static QuerySchema BuildSchema<T>(params string[] keyProps) where T : class
    {
        var schema = new QuerySchema
        {
            SourceType = typeof(T),
            TargetType = typeof(T),
            TopicName = typeof(T).Name.ToLowerInvariant(),
            IsValid = true,
            KeyProperties = keyProps
                .Select(p => PropertyMeta.FromProperty(typeof(T).GetProperty(p)!))
                .ToArray(),
            ValueProperties = typeof(T).GetProperties()
                .Select(p => PropertyMeta.FromProperty(p))
                .ToArray()
        };
        schema.KeyInfo.ClassName = typeof(T).Name + "Key";
        schema.KeyInfo.Namespace = typeof(T).Namespace ?? string.Empty;
        schema.ValueInfo.ClassName = typeof(T).Name + "Value";
        schema.ValueInfo.Namespace = typeof(T).Namespace ?? string.Empty;
        return schema;
    }

    [Fact]
    public void ToKeyValue_ReturnsKey()
    {
        var schema = BuildSchema<Sample>(nameof(Sample.Id));
        var entity = new Sample { Id = 1, Name = "a" };

        var result = PocoMapper.ToKeyValue(entity, schema);

        Assert.Equal(1, result.Key);
        Assert.Same(entity, result.Value);
    }

    [Fact]
    public void ToKeyValue_NullEntity_Throws()
    {
        var schema = BuildSchema<Sample>(nameof(Sample.Id));
        Assert.Throws<ArgumentNullException>(() => PocoMapper.ToKeyValue<Sample>(null!, schema));
    }

    [Fact]
    public void ToKeyValue_CompositeKey_ReturnsDictionary()
    {
        var schema = BuildSchema<CompositeSample>(nameof(CompositeSample.Id), nameof(CompositeSample.Code));
        var entity = new CompositeSample { Id = 7, Code = "x" };

        var result = PocoMapper.ToKeyValue(entity, schema);
        var dict = Assert.IsType<Dictionary<string, object>>(result.Key);
        Assert.Equal(7, dict[nameof(CompositeSample.Id)]);
        Assert.Equal("x", dict[nameof(CompositeSample.Code)]);
    }

    [Fact]
    public void FromKeyValue_SingleKey_SetsProperty()
    {
        var schema = BuildSchema<Sample>(nameof(Sample.Id));
        var entity = new Sample { Id = 0, Name = "a" };

        var merged = PocoMapper.FromKeyValue(5, entity, schema);

        Assert.Equal(5, merged.Id);
    }

    [Fact]
    public void FromKeyValue_CompositeKey_MergesDictionary()
    {
        var schema = BuildSchema<CompositeSample>(nameof(CompositeSample.Id), nameof(CompositeSample.Code));
        var entity = new CompositeSample { Id = 0, Code = "" };
        var key = new Dictionary<string, object> { ["Id"] = 3, ["Code"] = "z" };

        var merged = PocoMapper.FromKeyValue(key, entity, schema);

        Assert.Equal(3, merged.Id);
        Assert.Equal("z", merged.Code);
    }

    [Fact]
    public void ToKeyValue_NullCompositeValue_ReturnsEmptyString()
    {
        var schema = BuildSchema<NullComposite>(nameof(NullComposite.Id), nameof(NullComposite.Code));
        var entity = new NullComposite { Id = 5, Code = null };

        var result = PocoMapper.ToKeyValue(entity, schema);
        var dict = Assert.IsType<Dictionary<string, object>>(result.Key);
        Assert.Equal(string.Empty, dict[nameof(NullComposite.Code)]);
    }

    [Fact]
    public void ToKeyValue_UnsupportedKeyType_Throws()
    {
        var schema = BuildSchema<UnsupportedSample>(nameof(UnsupportedSample.Id));
        var entity = new UnsupportedSample { Id = 1m };

        Assert.Throws<NotSupportedException>(() => PocoMapper.ToKeyValue(entity, schema));
    }
}
