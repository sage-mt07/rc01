using Kafka.Ksql.Linq.Serialization.Avro.Core;
using Kafka.Ksql.Linq.Serialization.Avro.Management;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Serialization;

public class AvroSchemaRepositoryTests
{
    private class Sample
    {
        public int Id { get; set; }
    }

    [Fact]
    public void StoreAndRetrieve_Works()
    {
        var repo = new AvroSchemaRepository();
        var info = new AvroSchemaInfo
        {
            EntityType = typeof(Sample),
            TopicName = "t",
            KeySchemaId = 1,
            ValueSchemaId = 2
        };

        repo.StoreSchemaInfo(info);

        Assert.True(repo.IsRegistered(typeof(Sample)));
        Assert.Same(info, repo.GetSchemaInfo(typeof(Sample)));
        Assert.Same(info, repo.GetSchemaInfoByTopic("t"));
        Assert.Contains(info, repo.GetAllSchemas());

        repo.Clear();
        Assert.False(repo.IsRegistered(typeof(Sample)));
        Assert.Empty(repo.GetAllSchemas());
    }
}
