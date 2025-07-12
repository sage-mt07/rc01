using Kafka.Ksql.Linq.Core.Modeling;
using Kafka.Ksql.Linq.Mapping;
using System;
using System.Collections.Generic;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Mapping;

public class MappingManagerTests
{
    private class Sample
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private class CompositeSample
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
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

    [Fact]
    public void ExtractKeyValue_UnregisteredEntity_Throws()
    {
        var manager = new MappingManager();
        var entity = new Sample { Id = 1 };

        Assert.Throws<InvalidOperationException>(() => manager.ExtractKeyValue(entity));
    }

    [Fact]
    public void ExtractKeyValue_NullEntity_Throws()
    {
        var manager = new MappingManager();

        Assert.Throws<ArgumentNullException>(() => manager.ExtractKeyValue<Sample>(null!));
    }

    [Fact]
    public void ExtractKeyValue_WithCompositeKey_ReturnsDictionary()
    {
        var builder = new ModelBuilder();
        builder.Entity<CompositeSample>()
            .WithTopic("composite-topic")
            .HasKey(e => new { e.Id, e.Code });
        var model = builder.GetEntityModel<CompositeSample>()!;

        var manager = new MappingManager();
        manager.Register<CompositeSample>(model);

        var entity = new CompositeSample { Id = 7, Code = "x" };
        var result = manager.ExtractKeyValue(entity);
        var dict = Assert.IsType<Dictionary<string, object>>(result.Key);
        Assert.Equal(7, dict[nameof(CompositeSample.Id)]);
        Assert.Equal("x", dict[nameof(CompositeSample.Code)]);
    }

    [Fact]
    public void Register_SameEntityTwice_OverridesModel()
    {
        var b1 = new ModelBuilder();
        b1.Entity<Sample>().WithTopic("t1").HasKey(e => e.Id);
        var m1 = b1.GetEntityModel<Sample>()!;

        var b2 = new ModelBuilder();
        b2.Entity<Sample>().WithTopic("t2").HasKey(e => e.Name);
        var m2 = b2.GetEntityModel<Sample>()!;

        var manager = new MappingManager();
        manager.Register<Sample>(m1);
        manager.Register<Sample>(m2);

        var entity = new Sample { Id = 1, Name = "B" };
        var result = manager.ExtractKeyValue(entity);
        Assert.Equal("B", result.Key);
    }
}
