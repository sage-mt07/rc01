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


public class KsqlDbServiceDownTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ExecuteStatement_ShouldFail_WhenKsqlDbDown()
    {
        if (!KsqlDbAvailability.IsAvailable())
            throw new SkipException(KsqlDbAvailability.SkipReason);
        try
        {
            await TestEnvironment.ResetAsync();

        }
        catch (Exception ex)
        {
        }
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
