using Kafka.Ksql.Linq.Query.Abstractions;
using Kafka.Ksql.Linq.Query.Pipeline;
using System.Linq;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Integration;

public class QueryBuilderExecutionModeTests
{
    private class SimpleEntity
    {
        public int Id { get; set; }
        public double Amount { get; set; }
    }

    [Fact]
    public void AsPull_SetsExecutionModePull()
    {
        var qb = new QueryBuilder<SimpleEntity>()
            .FromSource<SimpleEntity>(src => src.Where(e => e.Id > 0))
            .AsPull();

        var schema = qb.GetSchema();
        Assert.True(schema.IsValid);
        Assert.Equal(QueryExecutionMode.PullQuery, schema.ExecutionMode);
    }

    [Fact]
    public void AsPush_SetsExecutionModePush()
    {
        var qb = new QueryBuilder<SimpleEntity>()
            .FromSource<SimpleEntity>(src => src.Where(e => e.Amount > 0))
            .AsPush();

        var schema = qb.GetSchema();
        Assert.True(schema.IsValid);
        Assert.Equal(QueryExecutionMode.PushQuery, schema.ExecutionMode);
    }
}
