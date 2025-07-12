using System;
using System.Linq;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Serialization.Abstractions;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Serialization;

public class AvroEntityConfigurationTests
{
    private class Order
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Debug { get; set; } = string.Empty;
    }

    [Fact]
    public void Constructor_SetsDefaults()
    {
        var cfg = new AvroEntityConfiguration(typeof(Order));
        Assert.Equal("order", cfg.TopicName);
        Assert.False(cfg.HasKeys());
    }

    [Fact]
    public void GetSerializableProperties_ReturnsAllProperties()
    {
        var cfg = new AvroEntityConfiguration(typeof(Order));
        var props = cfg.GetSerializableProperties();
        Assert.Contains(typeof(Order).GetProperty(nameof(Order.Debug))!, props);
    }

    [Fact]
    public void DetermineKeyType_ReturnsDefaultWhenNoKey()
    {
        var cfg = new AvroEntityConfiguration(typeof(Order));
        Assert.Equal(typeof(string), cfg.DetermineKeyType());
    }

    [Fact]
    public void GetSummary_IncludesInfo()
    {
        var cfg = new AvroEntityConfiguration(typeof(Order));
        var summary = cfg.GetSummary();
        Assert.Contains("Order", summary);
        Assert.Contains("order", summary);
    }
    private class MultiKey
    {
        public int B { get; set; }
        public int A { get; set; }
        public string Skip { get; set; } = string.Empty;
    }

    [Fact]
    public void Clone_CreatesEqualCopy()
    {
        var cfg = new AvroEntityConfiguration(typeof(MultiKey));
        var clone = cfg.Clone();
        Assert.True(cfg.Equals(clone));
        Assert.Equal(cfg.GetHashCode(), clone.GetHashCode());
        Assert.NotSame(cfg, clone);
    }

    [Fact]
    public void GetOrderedKeyProperties_SortsByOrder()
    {
        var cfg = new AvroEntityConfiguration(typeof(MultiKey));
        cfg.Configure<MultiKey>().HasKey(m => new { m.A, m.B });
        var props = cfg.GetOrderedKeyProperties();
        Assert.Equal(new[] { "A", "B" }, props.Select(p => p.Name));
    }

    [Fact]
    public void IsCompositeKey_ReturnsTrue()
    {
        var cfg = new AvroEntityConfiguration(typeof(MultiKey));
        cfg.Configure<MultiKey>().HasKey(m => new { m.A, m.B });
        Assert.True(cfg.IsCompositeKey());
    }

    [Fact]
    public void GetIgnoredProperties_ReturnsEmpty()
    {
        var cfg = new AvroEntityConfiguration(typeof(MultiKey));
        cfg.Configure<MultiKey>().HasKey(m => new { m.A, m.B });
        var props = cfg.GetIgnoredProperties();
        Assert.Empty(props);
    }

    [Fact]
    public void ToString_ReturnsSummary()
    {
        var cfg = new AvroEntityConfiguration(typeof(MultiKey));
        Assert.Contains("MultiKey", cfg.ToString());
    }

    [Fact]
    public void Validate_ErrorsOnBadKeyType()
    {
        var cfg = new AvroEntityConfiguration(typeof(BadKey));
        cfg.Configure<BadKey>().HasKey(b => b.Dt);
        var res = cfg.Validate();
        Assert.NotEmpty(res.Errors);
    }

    private class BadKey
    {
        public DateTime Dt { get; set; }
    }
}
