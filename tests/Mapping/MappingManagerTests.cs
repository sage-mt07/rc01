using Kafka.Ksql.Linq.Core.Modeling;
using Kafka.Ksql.Linq.Mapping;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Mapping;

public class MappingManagerTests
{
    private class Sample
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    [Fact]
    public void ExtractKeyValue_ReturnsRegisteredKey()
    {
        var modelBuilder = new ModelBuilder();
        modelBuilder.Entity<Sample>()
            .WithTopic("sample-topic")
            .HasKey(e => e.Id);
        var model = modelBuilder.GetEntityModel<Sample>()!;

        var manager = new MappingManager();
        manager.Register<Sample>(model);

        var entity = new Sample { Id = 1, Name = "a" };
        var result = manager.ExtractKeyValue(entity);

        Assert.Equal(1, result.Key);
        Assert.Same(entity, result.Value);
    }
}
