using Confluent.Kafka;
using Confluent.Kafka.Admin;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using Kafka.Ksql.Linq.Application;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Integration;

public class KafkaConnectivityTests
{
    [Fact]
    public async Task ProducerConsumer_RoundTrip()
    {
        var bootstrap = TestEnvironment.KafkaBootstrapServers;
        var topic = "connectivity_" + Guid.NewGuid().ToString("N");

        using (var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = bootstrap }).Build())
        {
            await admin.CreateTopicsAsync(new[] { new TopicSpecification { Name = topic, NumPartitions = 1, ReplicationFactor = 1 } });
        }

        using var producer = new ProducerBuilder<Null, string>(new ProducerConfig { BootstrapServers = bootstrap }).Build();
        await producer.ProduceAsync(topic, new Message<Null, string> { Value = "ok" });
        producer.Flush(TimeSpan.FromSeconds(10));

        using var consumer = new ConsumerBuilder<Null, string>(new ConsumerConfig
        {
            BootstrapServers = bootstrap,
            GroupId = Guid.NewGuid().ToString(),
            AutoOffsetReset = AutoOffsetReset.Earliest
        }).Build();

        consumer.Subscribe(topic);
        var msg = consumer.Consume(TimeSpan.FromSeconds(10));
        consumer.Close();

        Assert.NotNull(msg);
        Assert.Equal("ok", msg.Message.Value);

        await using var ctx = TestEnvironment.CreateContext();
        var result = await ctx.ExecuteStatementAsync("SHOW TOPICS;");
        Assert.True(result.IsSuccess);

 
    }
}

