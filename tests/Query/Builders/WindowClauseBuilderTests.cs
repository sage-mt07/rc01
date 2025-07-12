using System;
using System.Linq.Expressions;
using Kafka.Ksql.Linq;
using Kafka.Ksql.Linq.Query.Builders;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Query.Builders;

public class WindowClauseBuilderTests
{
    [Fact]
    public void Build_WithTimeSpanConstant_ReturnsTumblingClause()
    {
        var expr = Expression.Constant(TimeSpan.FromMinutes(5));
        var builder = new WindowClauseBuilder();

        var sql = builder.Build(expr);
        Assert.Equal("TUMBLING (SIZE 5 MINUTES)", sql);
    }

    [Fact]
    public void Build_SessionWindow_ReturnsSessionClause()
    {
        var def = SessionWindow.OfMinutes(2);
        var expr = Expression.Constant(def);
        var builder = new WindowClauseBuilder();

        var sql = builder.Build(expr);
        Assert.Equal("SESSION (GAP 2 MINUTES)", sql);
    }

    [Fact]
    public void Build_SessionWindowMissingGap_Throws()
    {
        var def = new WindowDef().SessionWindow();
        var expr = Expression.Constant(def);
        var builder = new WindowClauseBuilder();

        Assert.Throws<InvalidOperationException>(() => builder.Build(expr));
    }

    [Fact]
    public void Build_InvalidExpressionType_Throws()
    {
        var expr = Expression.Constant(123);
        var builder = new WindowClauseBuilder();

        Assert.Throws<InvalidOperationException>(() => builder.Build(expr));
    }
}
