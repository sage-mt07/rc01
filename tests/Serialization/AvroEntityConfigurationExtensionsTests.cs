using System;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Serialization.Abstractions;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Serialization;

public class AvroEntityConfigurationExtensionsTests
{
    private class Sample
    {
        public int Id { get; set; }
    }

    [Fact]
    public void Configure_WrongType_Throws()
    {
        var cfg = new AvroEntityConfiguration(typeof(Sample));
        Assert.Throws<ArgumentException>(() => cfg.Configure<string>());
    }

    [Fact]
    public void Configure_ReturnsBuilder()
    {
        var cfg = new AvroEntityConfiguration(typeof(Sample));
        var builder = cfg.Configure<Sample>();
        Assert.NotNull(builder);
    }

    [Fact]
    public void IsStreamType_And_IsTableType()
    {
        var cfg = new AvroEntityConfiguration(typeof(Sample));
        cfg.CustomSettings["StreamTableType"] = "Stream";
        Assert.True(cfg.IsStreamType());
        cfg.CustomSettings["StreamTableType"] = "Table";
        Assert.True(cfg.IsTableType());
    }
}
