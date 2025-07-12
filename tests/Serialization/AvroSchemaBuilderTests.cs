using Kafka.Ksql.Linq.Serialization.Avro.Management;
using System.Threading.Tasks;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Serialization;

public class AvroSchemaBuilderTests
{
    private class Sample
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    [Fact]
    public void GenerateKeySchema_ForSingleKey_ReturnsPrimitiveSchema()
    {
        var builder = new AvroSchemaBuilder();
        var schema = builder.GenerateKeySchema<Sample>();
        Assert.Contains("string", schema);
    }

    [Fact]
    public async Task ValidateSchemaAsync_InvalidSchema_ReturnsFalse()
    {
        var builder = new AvroSchemaBuilder();
        var result = await builder.ValidateSchemaAsync("{ invalid }");
        Assert.False(result);
    }
}
