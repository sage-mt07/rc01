using Kafka.Ksql.Linq.Query.Pipeline;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Query.Pipeline;

public class ExpressionAnalysisResultTests
{
    [Fact]
    public void AnalyzeLinqExpression_DetectsFlags()
    {
        IQueryable<TestEntity> data = new List<TestEntity>().AsQueryable();
        var query = data.Window(5).GroupBy(e => e.Id).Select(g => g.Sum(e => e.Id));

        var generator = new DMLQueryGenerator();
        var result = PrivateAccessor.InvokePrivate<ExpressionAnalysisResult>(generator, "AnalyzeLinqExpression", new[] { typeof(Expression) }, args: new object[] { query.Expression });

        Assert.True(result.HasGroupBy);
        Assert.True(result.HasAggregation);
        Assert.True(result.HasWindow);
    }

    [Fact]
    public void AnalyzeLinqExpression_NoAggregation()
    {
        IQueryable<TestEntity> data = new List<TestEntity>().AsQueryable();
        var query = data.Where(e => e.Id > 0);
        var generator = new DMLQueryGenerator();
        var result = PrivateAccessor.InvokePrivate<ExpressionAnalysisResult>(generator, "AnalyzeLinqExpression", new[] { typeof(Expression) }, args: new object[] { query.Expression });

        Assert.False(result.HasGroupBy);
        Assert.False(result.HasAggregation);
        Assert.False(result.HasWindow);
    }

    private class TestEntity
    {
        public int Id { get; set; }
    }
}
