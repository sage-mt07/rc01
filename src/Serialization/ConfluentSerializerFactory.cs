namespace Kafka.Ksql.Linq.Serialization;

using Confluent.Kafka;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using Kafka.Ksql.Linq.Serialization.Abstractions;
using System;

/// <summary>
/// Factory that creates Confluent Avro serializers and deserializers.
/// </summary>
public class ConfluentSerializerFactory : ISerializerFactory, IDeserializerFactory
{
    private readonly ISchemaRegistryClient _client;
    private readonly AvroSerializerConfig _serializerConfig;
    private readonly AvroDeserializerConfig _deserializerConfig;

    public ConfluentSerializerFactory(
        ISchemaRegistryClient client,
        AvroSerializerConfig? serializerConfig = null,
        AvroDeserializerConfig? deserializerConfig = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _serializerConfig = serializerConfig ?? new AvroSerializerConfig();
        _deserializerConfig = deserializerConfig ?? new AvroDeserializerConfig();
    }

    /// <inheritdoc />
    public ISerializer<T> CreateSerializer<T>()
    {
        return new AvroSerializer<T>(_client, _serializerConfig);
    }

    /// <inheritdoc />
    public IDeserializer<T> CreateDeserializer<T>()
    {
        return new AvroDeserializer<T>(_client, _deserializerConfig);
    }
}
