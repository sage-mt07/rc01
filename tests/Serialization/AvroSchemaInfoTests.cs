using Kafka.Ksql.Linq.Serialization.Avro.Core;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Serialization;

public class AvroSchemaInfoTests
{
    [Fact]
    public void HasCustomKey_And_KeyType_Work()
    {
        var info = new AvroSchemaInfo
        {
            EntityType = typeof(TestEntity),
            TopicName = "t",
            KeyProperties = new[] { typeof(TestEntity).GetProperty(nameof(TestEntity.Id))! }
        };
        Assert.True(info.HasCustomKey);
        Assert.Equal(typeof(int), info.KeyType);
    }

    [Fact]
    public void ToString_ContainsInfo()
    {
        var info = new AvroSchemaInfo
        {
            EntityType = typeof(TestEntity),
            TopicName = "topic",
            KeySchemaId = 1,
            ValueSchemaId = 2
        };
        var str = info.ToString();
        Assert.Contains("topic", str);
        Assert.Contains("1", str);
        Assert.Contains("2", str);
    }
}
