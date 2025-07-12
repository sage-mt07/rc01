using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Core.Modeling;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.ModelBuilderTests;

public class NewApiSkeletonTests
{
    private class Sample
    {
        public int Id { get; set; }
    }

    [Fact]
    public void WithTopicAndHasKey_ConfiguresModel()
    {
        var builder = new ModelBuilder();
        builder.Entity<Sample>()
            .WithTopic("sample")
            .HasKey(e => e.Id);

        var model = builder.GetEntityModel<Sample>();
        Assert.NotNull(model);
        Assert.Equal("sample", model!.TopicName);
        Assert.Single(model.KeyProperties);
    }
}
