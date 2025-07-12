using Confluent.Kafka;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Integration;

public class KsqlSyntaxTests
{
    private readonly IKsqlClient _client;
    public KsqlSyntaxTests()
    {
        _client = new KsqlClient(new Uri("http://localhost:8088"));

        TestEnvironment.ResetAsync().GetAwaiter().GetResult();

        var r1 = _client.ExecuteStatementAsync(
            "CREATE STREAM IF NOT EXISTS source (id INT) WITH (KAFKA_TOPIC='source', VALUE_FORMAT='AVRO', PARTITIONS=1);"
        ).Result;
        Console.WriteLine($"CREATE STREAM result: {r1.IsSuccess}, msg: {r1.Message}");

        foreach (var ddl in TestSchema.GenerateTableDdls())
        {
            var r = _client.ExecuteStatementAsync(ddl).Result;
            Console.WriteLine($"DDL result: {r.IsSuccess}, msg: {r.Message}");
        }
    }

    [KsqlDbTheory]
    [Trait("Category", "Integration")]
    [InlineData("CREATE STREAM test_stream AS SELECT * FROM source EMIT CHANGES;")]
    [InlineData("SELECT CustomerId, COUNT(*) FROM orders GROUP BY CustomerId EMIT CHANGES;")]
    public async Task GeneratedQuery_IsValidInKsqlDb(string ksql)
    {

        var response = await _client.ExecuteExplainAsync(ksql);
        Assert.True(response.IsSuccess, $"{ksql} failed: {response.Message}");
    }

}
