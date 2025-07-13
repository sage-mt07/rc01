using Kafka.Ksql.Linq.Core.Models;
using Kafka.Ksql.Linq.Mapping;
using System.Linq;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Mapping;

public class KeyValueTypeMappingMethodsTests
{
    private class Sample
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    [Fact]
    public void ExtractAndCombine_RoundTrip_ReturnsEquivalentObject()
    {
        var registry = new MappingRegistry();
        var keyProps = new[] { PropertyMeta.FromProperty(typeof(Sample).GetProperty(nameof(Sample.Id))!) };
        var valueProps = typeof(Sample).GetProperties()
            .Select(p => PropertyMeta.FromProperty(p))
            .ToArray();

        var mapping = registry.Register(typeof(Sample), keyProps, valueProps);

        var sample = new Sample { Id = 5, Name = "x" };

        var keyObj = mapping.ExtractKey(sample);
        var valueObj = mapping.ExtractValue(sample);

        var restored = (Sample)mapping.CombineFromKeyValue(keyObj, valueObj, typeof(Sample));

        Assert.Equal(sample.Id, restored.Id);
        Assert.Equal(sample.Name, restored.Name);
    }
}
