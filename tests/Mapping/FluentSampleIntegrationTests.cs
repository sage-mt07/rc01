using Kafka.Ksql.Linq.Entities.Samples;
using Kafka.Ksql.Linq.Entities.Samples.Models;
using SampleOrder = Kafka.Ksql.Linq.Entities.Samples.Models.Order;
using Kafka.Ksql.Linq.Mapping;
using Kafka.Ksql.Linq.Query.Schema;
using Kafka.Ksql.Linq.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
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

        var order = new SampleOrder { OrderId = 10, UserId = 20, ProductId = 30, Quantity = 1 };
        var schema = new QuerySchema
        {
            SourceType = typeof(SampleOrder),
            TargetType = typeof(SampleOrder),
            TopicName = "orders",
            IsValid = true,
            KeyProperties = new[]
            {
                PropertyMeta.FromProperty(typeof(SampleOrder).GetProperty(nameof(SampleOrder.OrderId))!),
                PropertyMeta.FromProperty(typeof(SampleOrder).GetProperty(nameof(SampleOrder.UserId))!)
            },
            ValueProperties = typeof(SampleOrder).GetProperties()
                .Select(p => PropertyMeta.FromProperty(p))
                .ToArray()
        };
        schema.KeyInfo.ClassName = "OrderKey";
        schema.KeyInfo.Namespace = typeof(SampleOrder).Namespace ?? string.Empty;
        schema.ValueInfo.ClassName = "OrderValue";
        schema.ValueInfo.Namespace = typeof(SampleOrder).Namespace ?? string.Empty;

        var result = PocoMapper.ToKeyValue(order, schema);

        var key = Assert.IsType<Dictionary<string, object>>(result.Key);
        Assert.Equal(10, key["OrderId"]);
        Assert.Equal(20, key["UserId"]);
        Assert.Same(order, result.Value);
    }
}
