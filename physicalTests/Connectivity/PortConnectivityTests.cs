using Confluent.Kafka;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using Kafka.Ksql.Linq.Application;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Integration;

[TestCaseOrderer("Kafka.Ksql.Linq.Tests.Integration.PriorityOrderer", "Kafka.Ksql.Linq.Tests.Integration")]
public class PortConnectivityTests
{
[Fact]
[TestPriority(1)]
    public void Kafka_Broker_Should_Be_Reachable()
    {
        using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = TestEnvironment.KafkaBootstrapServers }).Build();
        var meta = admin.GetMetadata(TimeSpan.FromSeconds(10));
        Assert.NotEmpty(meta.Brokers);
    }

[Fact]
[TestPriority(2)]
    public async Task SchemaRegistry_Should_Be_Reachable()
    {
        using var http = new HttpClient();
        var resp = await http.GetAsync($"{TestEnvironment.SchemaRegistryUrl}/subjects");
        Assert.True(resp.IsSuccessStatusCode);
    }

[Fact]
[TestPriority(3)]
    public async Task KsqlDb_Should_Be_Reachable()
    {
        await using var ctx = TestEnvironment.CreateContext();
        var result = await ctx.ExecuteStatementAsync("SHOW TOPICS;");
        Assert.True(result.IsSuccess);
    }
}
