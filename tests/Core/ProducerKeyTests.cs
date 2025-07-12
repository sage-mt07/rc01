using Kafka.Ksql.Linq.Core.Models;
using System;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Core;

public class ProducerKeyTests
{
    [Fact]
    public void Constructor_AssignsProperties()
    {
        var key = new ProducerKey(typeof(string), "topic", 123);
        Assert.Equal(typeof(string), key.EntityType);
        Assert.Equal("topic", key.TopicName);
        Assert.Equal(123, key.ConfigurationHash);
    }

    [Fact]
    public void Equals_WithSameValues_ReturnsTrue()
    {
        var a = new ProducerKey(typeof(int), "t", 1);
        var b = new ProducerKey(typeof(int), "t", 1);
        Assert.True(a.Equals(b));
        Assert.True(a.Equals((object)b));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Constructor_NullArguments_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new ProducerKey(null!, "t", 0));
        Assert.Throws<ArgumentNullException>(() => new ProducerKey(typeof(int), null!, 0));
    }

    [Fact]
    public void ToString_ReturnsFormattedString()
    {
        var key = new ProducerKey(typeof(int), "t", 0xABCD);
        var text = key.ToString();
        Assert.Contains("Int32", text);
        Assert.Contains("t", text);
        Assert.Contains("ABCD", text);
    }
}
