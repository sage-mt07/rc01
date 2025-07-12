using Kafka.Ksql.Linq.Core.Window;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Xunit;

#nullable enable

namespace Kafka.Ksql.Linq.Tests.Core.Window;

public class GroupByExpressionVisitorTests
{
    private class Sample
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    [Fact]
    public void Visit_SingleProperty_AddsUppercaseName()
    {
        Expression<Func<Sample, object>> expr = x => x.Id;
        var visitor = new GroupByExpressionVisitor();

        visitor.Visit(expr.Body);

        Assert.Contains("ID", visitor.GetColumns());
    }

    [Fact]
    public void Visit_AnonymousType_AddsAllMembers()
    {
        Expression<Func<Sample, object>> expr = x => new { x.Id, x.Name };
        var visitor = new GroupByExpressionVisitor();

        visitor.Visit(expr.Body);

        var cols = visitor.GetColumns();
        Assert.Contains("ID", cols);
        Assert.Contains("NAME", cols);
    }
}
