using System;
using System.Linq;
using System.Reflection;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Serialization.Abstractions;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Serialization;

public class AvroEntityConfigurationBuilderTests
{
    private class Sample
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    [Fact]
    public void Constructor_NullConfiguration_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new AvroEntityConfigurationBuilder<Sample>(null!));
    }

    private AvroEntityConfigurationBuilder<Sample> CreateBuilder()
    {
        var cfg = new AvroEntityConfiguration(typeof(Sample));
        return new AvroEntityConfigurationBuilder<Sample>(cfg);
    }

    [Fact]
    public void ToTopic_SetsName()
    {
        var cfg = new AvroEntityConfiguration(typeof(Sample));
        var builder = new AvroEntityConfigurationBuilder<Sample>(cfg);
        var returned = builder.ToTopic("t1");
        Assert.Same(builder, returned);
        Assert.Equal("t1", cfg.TopicName);
    }

    [Fact]
    public void HasKey_SetsKeyProperties()
    {
        var cfg = new AvroEntityConfiguration(typeof(Sample));
        var builder = new AvroEntityConfigurationBuilder<Sample>(cfg);
        builder.HasKey(s => s.Id);
        Assert.Single(cfg.KeyProperties!);
        Assert.Equal(nameof(Sample.Id), cfg.KeyProperties![0].Name);
    }

    [Fact]
    public void UsePropertyOrderKeys_SetsKeysByOrder()
    {
        var cfg = new AvroEntityConfiguration(typeof(Sample));
        var builder = new AvroEntityConfigurationBuilder<Sample>(cfg);
        builder.UsePropertyOrderKeys(2);
        Assert.Equal(2, cfg.KeyProperties.Length);
        Assert.Equal(nameof(Sample.Id), cfg.KeyProperties[0].Name);
        Assert.Equal(nameof(Sample.Name), cfg.KeyProperties[1].Name);
    }

    [Fact]
    public void WithPartitions_SetsValue()
    {
        var cfg = new AvroEntityConfiguration(typeof(Sample));
        var builder = new AvroEntityConfigurationBuilder<Sample>(cfg);
        builder.WithPartitions(3);
        Assert.Equal(3, cfg.Partitions);
    }

    [Fact]
    public void WithReplicationFactor_SetsValue()
    {
        var cfg = new AvroEntityConfiguration(typeof(Sample));
        var builder = new AvroEntityConfigurationBuilder<Sample>(cfg);
        builder.WithReplicationFactor(2);
        Assert.Equal(2, cfg.ReplicationFactor);
    }

    [Fact]
    public void ValidateOnStartup_SetsFlag()
    {
        var cfg = new AvroEntityConfiguration(typeof(Sample));
        var builder = new AvroEntityConfigurationBuilder<Sample>(cfg);
        builder.ValidateOnStartup(false);
        Assert.False(cfg.ValidateOnStartup);
    }

    [Fact]
    public void EnableCaching_SetsFlag()
    {
        var cfg = new AvroEntityConfiguration(typeof(Sample));
        var builder = new AvroEntityConfigurationBuilder<Sample>(cfg);
        builder.EnableCaching(false);
        Assert.False(cfg.EnableCaching);
    }

    [Fact]
    public void AsStream_SetsCustomSetting()
    {
        var builder = CreateBuilder();
        builder.AsStream();
        var cfg = builder.Build();
        Assert.True(cfg.CustomSettings.TryGetValue("StreamTableType", out var v));
        Assert.Equal("Stream", v);
    }

    [Fact]
    public void AsTable_SetsCustomSettingAndCaching()
    {
        var builder = CreateBuilder();
        builder.AsTable();
        var cfg = builder.Build();
        Assert.True(cfg.CustomSettings.TryGetValue("StreamTableType", out var v));
        Assert.Equal("Table", v);
        Assert.True(cfg.EnableCaching);
    }

    [Fact]
    public void AsTable_DisablesCachingWhenSpecified()
    {
        var builder = CreateBuilder();
        builder.AsTable(useCache: false);
        var cfg = builder.Build();
        Assert.False(cfg.EnableCaching);
    }

    [Fact]
    public void AsTable_SetsTopicNameWhenProvided()
    {
        var builder = CreateBuilder();
        builder.AsTable(topicName: "topic-a");
        var cfg = builder.Build();
        Assert.Equal("topic-a", cfg.TopicName);
    }


    [Fact]
    public void Build_ReturnsConfiguration()
    {
        var cfg = new AvroEntityConfiguration(typeof(Sample));
        var builder = new AvroEntityConfigurationBuilder<Sample>(cfg);
        Assert.Same(cfg, builder.Build());
    }

    [Fact]
    public void ExtractProperties_SupportsComposite()
    {
        var cfg = new AvroEntityConfiguration(typeof(Sample));
        var builder = new AvroEntityConfigurationBuilder<Sample>(cfg);
        var method = typeof(AvroEntityConfigurationBuilder<Sample>).GetMethod("ExtractProperties", BindingFlags.NonPublic | BindingFlags.Instance)!;
        method = method.MakeGenericMethod(typeof(object));
        var props = (PropertyInfo[])method.Invoke(builder, new object[] { (System.Linq.Expressions.Expression<Func<Sample, object>>) (s => new { s.Id, s.Name }) })!;
        Assert.Equal(2, props.Length);
        Assert.Contains(props, p => p.Name == nameof(Sample.Id));
        Assert.Contains(props, p => p.Name == nameof(Sample.Name));
    }
}
