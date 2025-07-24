using Kafka.Ksql.Linq;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Core.Modeling;
using Kafka.Ksql.Linq.Configuration;
using System;
using System.Linq.Expressions;
using Xunit;

namespace Kafka.Ksql.Linq.Tests;

public class ApplyModelBuilderSettingsTests
{
    private class Sample
    {
        public int Id { get; set; }
        public DateTime Time { get; set; }
    }

    private class TestContext : KsqlContext
    {
        public TestContext() : base(new KsqlDslOptions()) { }
        protected override bool SkipSchemaRegistration => true;

        protected override void OnModelCreating(IModelBuilder modelBuilder)
        {
            // Auto-register by accessing the set before configuring
            Set<Sample>();

            var builder = (EntityModelBuilder<Sample>)modelBuilder.Entity<Sample>()
                .AsTable(useCache: false)
                .WithManualCommit();
            builder.OnError(ErrorAction.DLQ);
            var model = builder.GetModel();
            model.DeserializationErrorPolicy = DeserializationErrorPolicy.DLQ;
            model.BarTimeSelector = (Expression<Func<Sample, DateTime>>)(x => x.Time);
        }
    }

    [Fact]
    public void AutoRegisteredModel_PropertiesFromModelBuilderAreApplied()
    {
        var ctx = new TestContext();
        var model = ctx.GetEntityModels()[typeof(Sample)];

        Assert.True(model.UseManualCommit);
        Assert.Equal(ErrorAction.DLQ, model.ErrorAction);
        Assert.Equal(DeserializationErrorPolicy.DLQ, model.DeserializationErrorPolicy);
        Assert.False(model.EnableCache);
        Assert.NotNull(model.BarTimeSelector);
    }
}
