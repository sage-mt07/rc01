using System;
using System.Linq;
using System.Linq.Expressions;
using Kafka.Ksql.Linq;
using Kafka.Ksql.Linq.Query.Builders;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Query.Builders.Visitors;

public class WindowExpressionVisitorTests
{
    private static MethodCallExpression ToWindowCall<T>(Expression<Func<IQueryable<T>, IQueryable<T>>> expr)
    {
        return (MethodCallExpression)expr.Body;
    }

    [Fact]
    public void ProcessWindow_Tumbling_ReturnsTumblingClause()
    {
        var mc = ToWindowCall<Order>(q => q.Window(TumblingWindow.OfMinutes(5)));
        var clause = WindowExpressionVisitorExtensions.ProcessWindowOperation(mc);
        Assert.Equal("WINDOW TUMBLING (SIZE 5 MINUTES)", clause);
    }

    [Fact]
    public void ProcessWindow_Hopping_ReturnsHoppingClause()
    {
        var mc = ToWindowCall<Order>(q => q.Window(HoppingWindow.OfMinutes(5).AdvanceBy(TimeSpan.FromMinutes(1))));
        var clause = WindowExpressionVisitorExtensions.ProcessWindowOperation(mc);
        Assert.Equal("WINDOW HOPPING (SIZE 5 MINUTES, ADVANCE BY 1 MINUTES)", clause);
    }

    [Fact]
    public void ProcessWindow_Session_ReturnsSessionClause()
    {
        var mc = ToWindowCall<Order>(q => q.Window(SessionWindow.OfMinutes(10)));
        var clause = WindowExpressionVisitorExtensions.ProcessWindowOperation(mc);
        Assert.Equal("WINDOW SESSION (GAP 10 MINUTES)", clause);
    }

    [Fact]
    public void ProcessWindow_InvalidSize_ThrowsNotSupported()
    {
        var mc = ToWindowCall<Order>(q => q.Window(TumblingWindow.OfMinutes(0)));
        Assert.Throws<NotSupportedException>(() => WindowExpressionVisitorExtensions.ProcessWindowOperation(mc));
    }

    [Fact]
    public void ProcessWindow_MultipleCalls_ThrowsInvalidOp()
    {
        Expression<Func<IQueryable<Order>, IQueryable<Order>>> expr = q => q.Window(TumblingWindow.OfMinutes(1)).Window(TumblingWindow.OfMinutes(2));
        var mc = (MethodCallExpression)expr.Body;
        Assert.Throws<InvalidOperationException>(() => WindowExpressionVisitorExtensions.ProcessWindowOperation(mc));
    }

    private class Order { }
}
