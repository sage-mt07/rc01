using Confluent.Kafka;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using Kafka.Ksql.Linq;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Integration;

public class KsqlDbServiceDownTests
{
    [KsqlDbFact]
    [Trait("Category", "Integration")]
    public async Task ExecuteStatement_ShouldFail_WhenKsqlDbDown()
    {
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
