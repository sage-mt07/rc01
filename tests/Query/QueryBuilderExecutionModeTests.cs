using Kafka.Ksql.Linq.Query.Abstractions;
using Kafka.Ksql.Linq.Query.Pipeline;
using System.Linq;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Query;

public class QueryBuilderExecutionModeTests
{
    private class SimpleEntity
    {
        public int Id { get; set; }
        public double Amount { get; set; }
    }

    // QueryBuilder.AsPull の動作確認: PullQuery が設定されるかを検証
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

    // QueryBuilder.AsPush の動作確認: PushQuery が設定されるかを検証
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
