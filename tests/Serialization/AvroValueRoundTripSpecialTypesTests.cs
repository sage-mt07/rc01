using System;
using System.Reflection;
using Confluent.SchemaRegistry;
using Confluent.Kafka;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Serialization.Avro.Core;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Serialization;

public class AvroValueRoundTripSpecialTypesTests
{
    private class SpecialEntity
    {
        public int Id { get; set; }
        public decimal Price { get; set; }
        public DateTime Timestamp { get; set; }
        public Guid Uid { get; set; }
    }

    [Fact]
    public void RoundTrip_SpecialTypes()
    {
        var client = DispatchProxy.Create<ISchemaRegistryClient, FakeSchemaRegistryClient>();
        var serializer = new AvroValueSerializer<SpecialEntity>(client);
        var deserializer = new AvroValueDeserializer<SpecialEntity>(client);

        var entity = new SpecialEntity
        {
            Id = 1,
            Price = 123.45m,
            Timestamp = new DateTime(2023,1,1,0,0,0,DateTimeKind.Utc),
            Uid = Guid.NewGuid()
        };

        var ctx = new SerializationContext(MessageComponentType.Value, "special-topic");
        var bytes = serializer.Serialize(entity, ctx);
        var result = (SpecialEntity)deserializer.Deserialize(bytes, false, ctx);

        Assert.Equal(entity.Id, result.Id);
        Assert.Equal(entity.Price, result.Price);
        Assert.Equal(entity.Timestamp, result.Timestamp);
        Assert.Equal(entity.Uid, result.Uid);
    }
}
