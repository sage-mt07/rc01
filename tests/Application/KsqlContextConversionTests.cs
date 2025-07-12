using Kafka.Ksql.Linq.Application;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Core.Context;
using Kafka.Ksql.Linq.Serialization.Abstractions;
using System;
using System.Collections.Generic;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Application;

public class KsqlContextConversionTests
{
    private class Sample
    {
        public int Id { get; set; }
    }

    private class TestContext : KsqlContext
    {
        public TestContext() : base() { }
        protected override bool SkipSchemaRegistration => true;
        public IReadOnlyDictionary<Type, AvroEntityConfiguration> Convert(Dictionary<Type, EntityModel> models)
            => base.ConvertToAvroConfigurations(models);
    }

    private static EntityModel CreateModel() => new()
    {
        EntityType = typeof(Sample),
        TopicName = "t",
        AllProperties = typeof(Sample).GetProperties(),
        KeyProperties = new[] { typeof(Sample).GetProperty(nameof(Sample.Id))! }
    };

    [Fact]
    public void ConvertToAvroConfigurations_CreatesConfiguration()
    {
        var ctx = new TestContext();
        var model = CreateModel();
        var result = ctx.Convert(new Dictionary<Type, EntityModel> { { typeof(Sample), model } });
        var cfg = Assert.Single(result).Value;
        Assert.Equal("t", cfg.TopicName);
        Assert.Equal(model.KeyProperties, cfg.KeyProperties);
    }
}
