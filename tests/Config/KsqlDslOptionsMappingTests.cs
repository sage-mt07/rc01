using Kafka.Ksql.Linq.Configuration;
using Kafka.Ksql.Linq.Core.Abstractions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Config;

public class KsqlDslOptionsMappingTests
{
    [Fact]
    public void AppSettings_ShouldMap_AllFields_To_KsqlDslOptions()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.ksqldsl.json", optional: false)
            .Build();

        var options = new KsqlDslOptions();
        configuration.GetSection("KsqlDsl").Bind(options);

        Assert.Equal(ValidationMode.Strict, options.ValidationMode);
        Assert.Equal("localhost:9092", options.Common.BootstrapServers);
        Assert.Equal("orders-consumer", options.Topics["orders"].Consumer.GroupId);
        Assert.Equal("http://localhost:8081", options.SchemaRegistry.Url);
        Assert.Equal("OrderEntity", options.Entities[0].Entity);
        Assert.Equal("dead.letter.queue", options.DlqTopicName);
        Assert.Equal(DeserializationErrorPolicy.DLQ, options.DeserializationErrorPolicy);
        Assert.True(options.ReadFromFinalTopicByDefault);
        Assert.Equal(38, options.DecimalPrecision);
        Assert.Equal(9, options.DecimalScale);
    }
}
