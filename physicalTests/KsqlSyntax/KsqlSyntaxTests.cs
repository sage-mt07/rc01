using Confluent.Kafka;
using System;
using Kafka.Ksql.Linq.Application;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Sdk;

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
    [Theory]
    [Trait("Category", "Integration")]
    [InlineData("CREATE STREAM test_stream AS SELECT * FROM source EMIT CHANGES;")]
    [InlineData("SELECT CustomerId, COUNT(*) FROM orders GROUP BY CustomerId EMIT CHANGES;")]
    public async Task GeneratedQuery_IsValidInKsqlDb(string ksql)
    {
        if (!KsqlDbAvailability.IsAvailable())
            throw new SkipException(KsqlDbAvailability.SkipReason);

        var response = await ExecuteExplainDirectAsync(ksql);
        Assert.True(response.IsSuccess, $"{ksql} failed: {response.Message}");
    }

    private static async Task<KsqlDbResponse> ExecuteExplainDirectAsync(string ksql)
    {
        using var client = new HttpClient { BaseAddress = new Uri(TestEnvironment.KsqlDbUrl) };
        var payload = new { ksql = $"EXPLAIN {ksql}", streamsProperties = new { } };
        var json = JsonSerializer.Serialize(payload);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/ksql", content);
        var body = await response.Content.ReadAsStringAsync();
        var success = response.IsSuccessStatusCode && !body.Contains("\"error_code\"");
        return new KsqlDbResponse(success, body);
    }

}
