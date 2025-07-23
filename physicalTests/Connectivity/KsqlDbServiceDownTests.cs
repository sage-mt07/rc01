using Confluent.Kafka;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Kafka.Ksql.Linq;
using Kafka.Ksql.Linq.Application;
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

public class KsqlDbServiceDownTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ExecuteStatement_ShouldFail_WhenKsqlDbDown()
    {
        if (!KsqlDbAvailability.IsAvailable())
            throw new SkipException(KsqlDbAvailability.SkipReason);

        await TestEnvironment.ResetAsync();
        await DockerHelper.StopServiceAsync("ksqldb-server");

        await using var ctx = TestEnvironment.CreateContext();
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await ctx.ExecuteStatementAsync("SHOW TOPICS;");
        });

        await DockerHelper.StartServiceAsync("ksqldb-server");
        await TestEnvironment.SetupAsync();
    }
}
