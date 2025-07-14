using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Core.Models;
using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Core;

public class KeyExtractorTests
{
    private class SampleEntity
    {
        public string Name { get; set; } = string.Empty;
        public int Id { get; set; }
        public string Data { get; set; } = string.Empty;
    }

    private static EntityModel CreateModel()
    {
        return new EntityModel
        {
            EntityType = typeof(SampleEntity),
            TopicName = "sample-topic",
            KeyProperties = new[]
            {
                typeof(SampleEntity).GetProperty(nameof(SampleEntity.Id))!,
                typeof(SampleEntity).GetProperty(nameof(SampleEntity.Name))!
            },
            AllProperties = typeof(SampleEntity).GetProperties()
        };
    }

    [Fact]
    public void IsCompositeKey_WithMultipleKeys_ReturnsTrue()
    {
        var model = CreateModel();
        Assert.True(KeyExtractor.IsCompositeKey(model));
    }

    [Fact]
    public void DetermineKeyType_WithCompositeKeys_ReturnsDictionaryType()
    {
        var model = CreateModel();
        Assert.Equal(typeof(Dictionary<string, object>), KeyExtractor.DetermineKeyType(model));
    }

    [Fact]
    public void ExtractKeyProperties_Count_ReturnsOrdered()
    {
        var props = KeyExtractor.ExtractKeyProperties(typeof(SampleEntity), 2);
        Assert.Equal(2, props.Length);
        Assert.Equal(nameof(SampleEntity.Name), props[0].Name);
        Assert.Equal(nameof(SampleEntity.Id), props[1].Name);
    }

    [Fact]
    public void ExtractKeyValue_WithEntity_ReturnsDictionary()
    {
        var entity = new SampleEntity { Id = 1, Name = "A" };
        var model = CreateModel();
        var value = KeyExtractor.ExtractKeyValue(entity, model);
        var dict = Assert.IsType<Dictionary<string, object>>(value);
        Assert.Equal(1, dict[nameof(SampleEntity.Id)]);
        Assert.Equal("A", dict[nameof(SampleEntity.Name)]);
    }

    [Fact]
    public void KeyToString_WithDictionary_ReturnsConcatenatedString()
    {
        var dict = new Dictionary<string, object>
        {
            ["Id"] = 1,
            ["Name"] = "A"
        };
        var str = KeyExtractor.KeyToString(dict);
        Assert.Contains("Id=1", str);
        Assert.Contains("Name=A", str);
    }

    [Fact]
    public void KeyToString_WithNull_ReturnsEmpty()
    {
        var result = KeyExtractor.KeyToString(null!);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void KeyToString_WithString_ReturnsSameString()
    {
        var result = KeyExtractor.KeyToString("abc");
        Assert.Equal("abc", result);
    }

    [Fact]
    public void KeyToString_WithObject_UsesToString()
    {
        var obj = new { X = 1 };
        var result = KeyExtractor.KeyToString(obj);
        Assert.Contains("X = 1", result);
    }

    [Fact]
    public void KeyToString_WithGuid_ReturnsGuidString()
    {
        var g = Guid.NewGuid();
        var result = KeyExtractor.KeyToString(g);
        Assert.Equal(g.ToString(), result);
    }

    [Fact]
    public void IsSupportedKeyType_ReturnsExpectedResults()
    {
        Assert.True(KeyExtractor.IsSupportedKeyType(typeof(int)));
        Assert.True(KeyExtractor.IsSupportedKeyType(typeof(Guid?)));
        Assert.False(KeyExtractor.IsSupportedKeyType(typeof(decimal)));
        Assert.False(KeyExtractor.IsSupportedKeyType(typeof(byte[])));
    }


    [Fact]
    public void ExtractKeyParts_WithCompositeKey_ReturnsParts()
    {
        var entity = new SampleEntity { Id = 1, Name = "A" };
        var model = CreateModel();
        var parts = KeyExtractor.ExtractKeyParts(entity, model);
        Assert.Equal(2, parts.Count);
        Assert.Equal("Id", parts[0].KeyName);
        Assert.Equal(typeof(int), parts[0].KeyType);
        Assert.Equal("1", parts[0].Value);
        Assert.Equal("Name", parts[1].KeyName);
        Assert.Equal(typeof(string), parts[1].KeyType);
        Assert.Equal("A", parts[1].Value);
    }

    [Fact]
    public void BuildTypedKey_RoundTrip_Works()
    {
        var entity = new SampleEntity { Id = 2, Name = "B" };
        var model = CreateModel();
        var parts = KeyExtractor.ExtractKeyParts(entity, model);
        var obj = KeyExtractor.BuildTypedKey(parts);
        var dict = Assert.IsType<Dictionary<string, object>>(obj);
        Assert.Equal(2, dict["Id"]);
        Assert.Equal("B", dict["Name"]);
    }

    [Fact]
    public void BuildTypedKey_InvalidValue_Throws()
    {
        var parts = new List<CompositeKeyPart> { new("Id", typeof(int), "abc") };
        Assert.ThrowsAny<Exception>(() => KeyExtractor.BuildTypedKey(parts));
    }
}
