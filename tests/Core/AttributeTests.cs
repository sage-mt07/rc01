using Kafka.Ksql.Linq.Core.Attributes;
using Kafka.Ksql.Linq.Core.Abstractions;
using System;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Core;

public class DefaultAndMaxLengthAttributeTests
{
    [Fact]
    public void DefaultValueAttribute_StoresValue()
    {
        var attr = new DefaultValueAttribute(123);
        Assert.Equal(123, attr.Value);
        Assert.Contains("123", attr.ToString());
    }

    [Fact]
    public void DefaultValueAttribute_Null_ShowsNullInToString()
    {
        var attr = new DefaultValueAttribute(null);
        Assert.Equal("DefaultValue: null", attr.ToString());
    }

    [Fact]
    public void MaxLengthAttribute_StoresLength()
    {
        var attr = new MaxLengthAttribute(5);
        Assert.Equal(5, attr.Length);
        Assert.Equal("MaxLength: 5", attr.ToString());
    }

    [Fact]
    public void MaxLengthAttribute_InvalidLength_Throws()
    {
        Assert.Throws<ArgumentException>(() => new MaxLengthAttribute(0));
    }
}

