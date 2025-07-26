using Kafka.Ksql.Linq.Configuration;
using Kafka.Ksql.Linq.Core.Modeling;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Application;
using System.Linq;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Query;

public class HasQueryFromTests
{
    private class ApiMessage
    {
        public string Category { get; set; } = string.Empty;
    }

    private class CategoryCount
    {
        public string Key { get; set; } = string.Empty;
        public long Count { get; set; }
    }

    private class TestContext : KsqlContext
    {
        public TestContext() : base(new KsqlDslOptions()) { }
        protected override bool SkipSchemaRegistration => true;

        protected override void OnModelCreating(IModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ApiMessage>();
            var catBuilder = (EntityModelBuilder<CategoryCount>)modelBuilder.Entity<CategoryCount>();
            EntityBuilderQueryExtensions.HasQueryFrom<ApiMessage, CategoryCount>(
                catBuilder,
                q => q.GroupBy(m => m.Category)
                      .Select(g => new CategoryCount { Key = g.Key, Count = g.Count() }));
        }
    }

    [Fact]
    public void HasQueryFrom_StoresSchema()
    {
        var ctx = new TestContext();
        var schema = ctx.GetQuerySchema<CategoryCount>();

        Assert.NotNull(schema);
        Assert.True(schema!.IsValid);
        Assert.Single(schema.KeyProperties);
        Assert.Equal(nameof(ApiMessage.Category), schema.KeyProperties[0].Name);
    }

    [Fact]
    public void DefineQueryFrom_RegistersSchema()
    {
        var builder = new ModelBuilder();
        builder.DefineQueryFrom<ApiMessage, CategoryCount>(q =>
            q.GroupBy(m => m.Category)
             .Select(g => new CategoryCount { Key = g.Key, Count = g.Count() }));

        var model = builder.GetEntityModel<CategoryCount>()!;
        Assert.NotNull(model.ValidationResult);
    }
}
