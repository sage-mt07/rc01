using System;
using System.Linq;
using System.Linq.Expressions;
using Kafka.Ksql.Linq;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Extensions;

public class WindowInfoAndOffsetExtensionsTests
{
    [Fact]
    public void WindowStart_ThrowsWhenInvoked()
    {
        var grouping = new[] { 1 }.GroupBy(x => x).First();
        Assert.Throws<NotSupportedException>(() => grouping.WindowStart());
    }

    [Fact]
    public void WindowEnd_ThrowsWhenInvoked()
    {
        var grouping = new[] { 1 }.GroupBy(x => x).First();
        Assert.Throws<NotSupportedException>(() => grouping.WindowEnd());
    }

    [Fact]
    public void LatestByOffset_ThrowsWhenInvoked()
    {
        var grouping = new[] { 1 }.GroupBy(x => x).First();
        Expression<Func<int, int>> selector = x => x;
        Assert.Throws<NotSupportedException>(() => grouping.LatestByOffset(selector));
    }

    [Fact]
    public void EarliestByOffset_ThrowsWhenInvoked()
    {
        var grouping = new[] { 1 }.GroupBy(x => x).First();
        Expression<Func<int, int>> selector = x => x;
        Assert.Throws<NotSupportedException>(() => grouping.EarliestByOffset(selector));
    }
}
