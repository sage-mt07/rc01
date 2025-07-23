using Confluent.Kafka;
using System;
using Kafka.Ksql.Linq.Application;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Sdk;

namespace Kafka.Ksql.Linq.Tests.Integration;

internal static class KsqlDbAvailability
{
    public const string SkipReason = "Skipped in CI due to missing ksqlDB instance or schema setup failure";
    private static bool _available;
    private static DateTime? _lastFailure;
    private static readonly object _sync = new();

    public static bool IsAvailable()
    {
        lock (_sync)
        {
            if (_available)
                return true;

            if (_lastFailure.HasValue && DateTime.UtcNow - _lastFailure.Value < TimeSpan.FromSeconds(5))
                return false;

            const int attempts = 3;
            for (var i = 0; i < attempts; i++)
            {
                try
                {
                    using var ctx = TestEnvironment.CreateContext();
                    var r = ctx.ExecuteStatementAsync("SHOW TOPICS;").GetAwaiter().GetResult();
                    if (r.IsSuccess)
                    {
                        _available = true;
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ksqlDB check attempt {i + 1} failed: {ex.Message}");
                }

                Thread.Sleep(1000);
            }

            _available = false;
            _lastFailure = DateTime.UtcNow;
            return false;
        }
    }
}

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

        await using var ctx = TestEnvironment.CreateContext();
        var response = await ctx.ExecuteExplainAsync(ksql);
        Assert.True(response.IsSuccess, $"{ksql} failed: {response.Message}");
    }

}
