using Confluent.Kafka;
using System;
using Kafka.Ksql.Linq.Application;
using System.Threading.Tasks;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Integration;

public class KsqlSyntaxTests
{
    public KsqlSyntaxTests()
    {
        TestEnvironment.ResetAsync().GetAwaiter().GetResult();

        using var ctx = TestEnvironment.CreateContext();
        var r1 = ctx.ExecuteStatementAsync(
            "CREATE STREAM IF NOT EXISTS source (id INT) WITH (KAFKA_TOPIC='source', VALUE_FORMAT='AVRO', PARTITIONS=1);"
        ).Result;
        Console.WriteLine($"CREATE STREAM result: {r1.IsSuccess}, msg: {r1.Message}");

        foreach (var ddl in TestSchema.GenerateTableDdls())
        {
            var r = ctx.ExecuteStatementAsync(ddl).Result;
            Console.WriteLine($"DDL result: {r.IsSuccess}, msg: {r.Message}");
        }
    }

    // 生成されたクエリがksqlDBで解釈可能か確認
    [KsqlDbTheory]
    [Trait("Category", "Integration")]
    [InlineData("CREATE STREAM test_stream AS SELECT * FROM source EMIT CHANGES;")]
    [InlineData("SELECT CustomerId, COUNT(*) FROM orders GROUP BY CustomerId EMIT CHANGES;")]
    public async Task GeneratedQuery_IsValidInKsqlDb(string ksql)
    {

        await using var ctx = TestEnvironment.CreateContext();
        var response = await ctx.ExecuteExplainAsync(ksql);
        Assert.True(response.IsSuccess, $"{ksql} failed: {response.Message}");
    }

}
