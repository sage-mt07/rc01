using Kafka.Ksql.Linq.Entities.Samples;
using Kafka.Ksql.Linq.Entities.Samples.Models;
using SampleOrder = Kafka.Ksql.Linq.Entities.Samples.Models.Order;
using Kafka.Ksql.Linq.Mapping;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using System.Collections.Generic;

namespace Kafka.Ksql.Linq.Tests.Mapping;

public class FluentSampleIntegrationTests
{
    [Fact]
    public void AddSampleModels_RegistersModelsInMappingManager()
    {
        var services = new ServiceCollection();
        services.AddSampleModels();
        var provider = services.BuildServiceProvider();

        var manager = provider.GetRequiredService<IMappingManager>();
        var order = new SampleOrder { OrderId = 10, UserId = 20, ProductId = 30, Quantity = 1 };

        var result = manager.ExtractKeyValue(order);

        var key = Assert.IsType<Dictionary<string, object>>(result.Key);
        Assert.Equal(10, key["OrderId"]);
        Assert.Equal(20, key["UserId"]);
        Assert.Same(order, result.Value);
    }
}
