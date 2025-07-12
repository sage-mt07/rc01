using Avro;
using Avro.Generic;
using Confluent.Kafka;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Serialization.Abstractions;
using Kafka.Ksql.Linq.Serialization.Avro.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ConfluentSchemaRegistry = Confluent.SchemaRegistry;

namespace Kafka.Ksql.Linq.Serialization.Avro.Core;

internal class AvroSerializerFactory
{
    private readonly ConfluentSchemaRegistry.ISchemaRegistryClient _schemaRegistryClient;
    private readonly ILogger<AvroSerializerFactory>? _logger;
    private readonly ILoggerFactory? _loggerFactory;
    
    // Schema caching for performance
    private static readonly ConcurrentDictionary<int, Schema> _avroSchemaCache = new();
    private static readonly ConcurrentDictionary<string, ConfluentSchemaRegistry.Serdes.AvroSerializer<GenericRecord>> _avroSerializerCache = new();
    private static readonly ConcurrentDictionary<string, ConfluentSchemaRegistry.Serdes.AvroDeserializer<GenericRecord>> _avroDeserializerCache = new();

    public AvroSerializerFactory(
        ConfluentSchemaRegistry.ISchemaRegistryClient schemaRegistryClient,
        ILoggerFactory? loggerFactory = null)
    {
        _schemaRegistryClient = schemaRegistryClient ?? throw new ArgumentNullException(nameof(schemaRegistryClient));
        _loggerFactory = loggerFactory;
        _logger = loggerFactory?.CreateLogger<AvroSerializerFactory>()
            ?? NullLogger<AvroSerializerFactory>.Instance;
    }

    public async Task<SerializerPair<T>> CreateSerializersAsync<T>(EntityModel entityModel, CancellationToken cancellationToken = default) where T : class
    {
        var keySchemaId = await RegisterKeySchemaAsync<T>(entityModel, cancellationToken);
        var valueSchemaId = await RegisterValueSchemaAsync<T>(entityModel, cancellationToken);

        var keySerializer = CreateKeySerializer<T>(entityModel, keySchemaId);
        var valueSerializer = CreateValueSerializer<T>(valueSchemaId);

        return new SerializerPair<T>
        {
            KeySerializer = keySerializer,
            ValueSerializer = valueSerializer,
            KeySchemaId = keySchemaId,
            ValueSchemaId = valueSchemaId
        };
    }

    public async Task<DeserializerPair<T>> CreateDeserializersAsync<T>(EntityModel entityModel, CancellationToken cancellationToken = default) where T : class
    {
        var keySchemaId = await RegisterKeySchemaAsync<T>(entityModel, cancellationToken);
        var valueSchemaId = await RegisterValueSchemaAsync<T>(entityModel, cancellationToken);

        var keyDeserializer = CreateKeyDeserializer<T>(entityModel, keySchemaId);
        var valueDeserializer = CreateValueDeserializer<T>(valueSchemaId);

        return new DeserializerPair<T>
        {
            KeyDeserializer = keyDeserializer,
            ValueDeserializer = valueDeserializer,
            KeySchemaId = keySchemaId,
            ValueSchemaId = valueSchemaId
        };
    }

    public IAvroSerializer<T> CreateSerializer<T>() where T : class
    {
        _logger?.LogDebug("Creating Avro serializer for type {Type}", typeof(T).Name);
        return new Core.AvroSerializer<T>(_loggerFactory);
    }

    public IAvroDeserializer<T> CreateDeserializer<T>() where T : class
    {
        _logger?.LogDebug("Creating Avro deserializer for type {Type}", typeof(T).Name);
        return new Core.AvroDeserializer<T>(_loggerFactory);
    }

    private async Task<int> RegisterKeySchemaAsync<T>(EntityModel entityModel, CancellationToken cancellationToken) where T : class
    {
        var topicName = (entityModel.TopicName ?? entityModel.EntityType.Name).ToLowerInvariant();
        var keyType = DetermineKeyType(entityModel);
        var keySchema = GenerateKeySchema(keyType, entityModel);

        var subject = $"{topicName}-key";
        var schema = new ConfluentSchemaRegistry.Schema(keySchema, ConfluentSchemaRegistry.SchemaType.Avro);

        return await _schemaRegistryClient.RegisterSchemaAsync(subject, schema, normalize: false);
    }

    private async Task<int> RegisterValueSchemaAsync<T>(EntityModel entityModel, CancellationToken cancellationToken) where T : class
    {
        var topicName = (entityModel.TopicName ?? entityModel.EntityType.Name).ToLowerInvariant();
        var valueSchema = GenerateValueSchema<T>();

        var subject = $"{topicName}-value";
        var schema = new ConfluentSchemaRegistry.Schema(valueSchema, ConfluentSchemaRegistry.SchemaType.Avro);

        return await _schemaRegistryClient.RegisterSchemaAsync(subject, schema, normalize: false);
    }

    private ISerializer<object> CreateKeySerializer<T>(EntityModel entityModel, int schemaId) where T : class
    {
        var topicName = (entityModel.TopicName ?? entityModel.EntityType.Name).ToLowerInvariant();
        var subject = $"{topicName}-key";

        if (IsCompositeKey(entityModel))
        {
            return new AvroCompositeKeySerializer(_schemaRegistryClient, subject, schemaId);
        }
        else
        {
            var keyType = DetermineKeyType(entityModel);
            return CreatePrimitiveKeySerializer(keyType);
        }
    }

    private ISerializer<object> CreateValueSerializer<T>(int schemaId) where T : class
    {
        return new AvroValueSerializer<T>(_schemaRegistryClient);
    }

    private IDeserializer<object> CreateKeyDeserializer<T>(EntityModel entityModel, int schemaId) where T : class
    {
        var topicName = (entityModel.TopicName ?? entityModel.EntityType.Name).ToLowerInvariant();
        var subject = $"{topicName}-key";

        if (IsCompositeKey(entityModel))
        {
            return new AvroCompositeKeyDeserializer(_schemaRegistryClient, subject);
        }
        else
        {
            var keyType = DetermineKeyType(entityModel);
            return CreatePrimitiveKeyDeserializer(keyType);
        }
    }

    private IDeserializer<object> CreateValueDeserializer<T>(int schemaId) where T : class
    {
        return new AvroValueDeserializer<T>(_schemaRegistryClient);
    }

    private Type DetermineKeyType(EntityModel entityModel)
    {
        if (entityModel.KeyProperties.Length == 0)
            return typeof(string);

        if (entityModel.KeyProperties.Length == 1)
            return entityModel.KeyProperties[0].PropertyType;

        return typeof(System.Collections.Generic.Dictionary<string, object>);
    }

    private bool IsCompositeKey(EntityModel entityModel)
    {
        return entityModel.KeyProperties.Length > 1;
    }

    private string GenerateKeySchema(Type keyType, EntityModel entityModel)
    {
        if (IsCompositeKey(entityModel))
        {
            return UnifiedSchemaGenerator.GenerateCompositeKeySchema(entityModel.KeyProperties);
        }
        else
        {
            return UnifiedSchemaGenerator.GenerateKeySchema(keyType);
        }
    }

    private string GenerateValueSchema<T>() where T : class
    {
        return UnifiedSchemaGenerator.GenerateSchema<T>();
    }

    private ISerializer<object> CreatePrimitiveKeySerializer(Type keyType)
    {
        if (keyType == typeof(string))
            return new StringKeySerializer();
        if (keyType == typeof(int))
            return new IntKeySerializer();
        if (keyType == typeof(long))
            return new LongKeySerializer();
        if (keyType == typeof(Guid))
            return new GuidKeySerializer();

        throw new NotSupportedException($"Key type {keyType.Name} is not supported");
    }

    private IDeserializer<object> CreatePrimitiveKeyDeserializer(Type keyType)
    {
        if (keyType == typeof(string))
            return new StringKeyDeserializer();
        if (keyType == typeof(int))
            return new IntKeyDeserializer();
        if (keyType == typeof(long))
            return new LongKeyDeserializer();
        if (keyType == typeof(Guid))
            return new GuidKeyDeserializer();

        throw new NotSupportedException($"Key type {keyType.Name} is not supported");
    }

    public static Schema GetAvroSchema(int schemaId, ConfluentSchemaRegistry.ISchemaRegistryClient client)
    {
        return _avroSchemaCache.GetOrAdd(schemaId, id =>
        {
            var registeredSchema = client.GetSchemaAsync(id)
                .ConfigureAwait(false).GetAwaiter().GetResult();
            return Schema.Parse(registeredSchema.SchemaString);
        });
    }

    public static ConfluentSchemaRegistry.Serdes.AvroSerializer<GenericRecord> GetAvroSerializer(string subject, ConfluentSchemaRegistry.ISchemaRegistryClient client)
    {
        return _avroSerializerCache.GetOrAdd(subject, _ =>
            new ConfluentSchemaRegistry.Serdes.AvroSerializer<GenericRecord>(client));
    }

    public static ConfluentSchemaRegistry.Serdes.AvroDeserializer<GenericRecord> GetAvroDeserializer(string subject, ConfluentSchemaRegistry.ISchemaRegistryClient client)
    {
        return _avroDeserializerCache.GetOrAdd(subject, _ =>
            new ConfluentSchemaRegistry.Serdes.AvroDeserializer<GenericRecord>(client));
    }
}

// Primitive Key Serializers (unchanged)
internal class StringKeySerializer : ISerializer<object>
{
    public byte[] Serialize(object data, SerializationContext context)
    {
        return System.Text.Encoding.UTF8.GetBytes(data?.ToString() ?? "");
    }
}

internal class StringKeyDeserializer : IDeserializer<object>
{
    public object Deserialize(ReadOnlySpan<byte> data, bool isNull, SerializationContext context)
    {
        if (isNull) return "";
        return System.Text.Encoding.UTF8.GetString(data);
    }
}

internal class IntKeySerializer : ISerializer<object>
{
    public byte[] Serialize(object data, SerializationContext context)
    {
        if (data is int value)
            return BitConverter.GetBytes(value);
        throw new InvalidOperationException($"Cannot serialize {data?.GetType().Name} as int key");
    }
}

internal class IntKeyDeserializer : IDeserializer<object>
{
    public object Deserialize(ReadOnlySpan<byte> data, bool isNull, SerializationContext context)
    {
        if (isNull) return 0;
        return BitConverter.ToInt32(data);
    }
}

internal class LongKeySerializer : ISerializer<object>
{
    public byte[] Serialize(object data, SerializationContext context)
    {
        if (data is long value)
            return BitConverter.GetBytes(value);
        throw new InvalidOperationException($"Cannot serialize {data?.GetType().Name} as long key");
    }
}

internal class LongKeyDeserializer : IDeserializer<object>
{
    public object Deserialize(ReadOnlySpan<byte> data, bool isNull, SerializationContext context)
    {
        if (isNull) return 0L;
        return BitConverter.ToInt64(data);
    }
}

internal class GuidKeySerializer : ISerializer<object>
{
    public byte[] Serialize(object data, SerializationContext context)
    {
        if (data is Guid value)
            return value.ToByteArray();
        throw new InvalidOperationException($"Cannot serialize {data?.GetType().Name} as Guid key");
    }
}

internal class GuidKeyDeserializer : IDeserializer<object>
{
    public object Deserialize(ReadOnlySpan<byte> data, bool isNull, SerializationContext context)
    {
        if (isNull) return Guid.Empty;
        return new Guid(data);
    }
}

// Modified Composite Key Serializer (Avro format)
internal class AvroCompositeKeySerializer : ISerializer<object>
{
    private readonly ConfluentSchemaRegistry.ISchemaRegistryClient _client;
    private readonly string _subject;
    private readonly int _schemaId;
    private Schema? _avroSchema;
    private ConfluentSchemaRegistry.Serdes.AvroSerializer<GenericRecord>? _avroSerializer;

    public AvroCompositeKeySerializer(ConfluentSchemaRegistry.ISchemaRegistryClient client, string subject, int schemaId)
    {
        _client = client;
        _subject = subject;
        _schemaId = schemaId;
    }

    public byte[] Serialize(object data, SerializationContext context)
    {
        if (data is not Dictionary<string, object> dict)
            throw new InvalidOperationException("Expected Dictionary<string, object> for composite key");

        EnsureInitialized();

        var record = CreateGenericRecord(dict);
        
        return _avroSerializer!.SerializeAsync(record, context)
            .ConfigureAwait(false).GetAwaiter().GetResult();
    }

    private void EnsureInitialized()
    {
        if (_avroSchema == null || _avroSerializer == null)
        {
            _avroSchema = AvroSerializerFactory.GetAvroSchema(_schemaId, _client);
            _avroSerializer = AvroSerializerFactory.GetAvroSerializer(_subject, _client);
        }
    }

    private GenericRecord CreateGenericRecord(Dictionary<string, object> dict)
    {
        // GenericRecordはRecordSchemaを必要とする
        if (_avroSchema is not RecordSchema recordSchema)
            throw new InvalidOperationException("Schema must be a RecordSchema for composite keys");

        var record = new GenericRecord(recordSchema);

        foreach (var field in recordSchema.Fields)
        {
            if (dict.TryGetValue(field.Name, out var value))
            {
                record.Add(field.Name, ConvertValue(value, field.Schema));
            }
            else
            {
                record.Add(field.Name, GetDefaultValue(field.Schema));
            }
        }

        return record;
    }

    private object? ConvertValue(object value, Schema fieldSchema)
    {
        return fieldSchema.Tag switch
        {
            Schema.Type.Int => Convert.ToInt32(value),
            Schema.Type.Long => Convert.ToInt64(value),
            Schema.Type.Float => Convert.ToSingle(value),
            Schema.Type.Double => Convert.ToDouble(value),
            Schema.Type.Boolean => Convert.ToBoolean(value),
            Schema.Type.String => value.ToString(),
            Schema.Type.Union => HandleUnionType(value, fieldSchema as UnionSchema),
            _ => value
        };
    }

    private object? HandleUnionType(object value, UnionSchema? unionSchema)
    {
        if (value == null || unionSchema == null)
            return null;
            
        foreach (var schema in unionSchema.Schemas)
        {
            if (schema.Tag != Schema.Type.Null)
            {
                return ConvertValue(value, schema);
            }
        }
        
        return value;
    }

    private object? GetDefaultValue(Schema fieldSchema)
    {
        return fieldSchema.Tag switch
        {
            Schema.Type.Int => 0,
            Schema.Type.Long => 0L,
            Schema.Type.Float => 0f,
            Schema.Type.Double => 0.0,
            Schema.Type.Boolean => false,
            Schema.Type.String => "",
            Schema.Type.Union => null,
            _ => null
        };
    }
}

// Modified Composite Key Deserializer (Avro format)
internal class AvroCompositeKeyDeserializer : IDeserializer<object>
{
    private readonly ConfluentSchemaRegistry.ISchemaRegistryClient _client;
    private readonly string _subject;
    private ConfluentSchemaRegistry.Serdes.AvroDeserializer<GenericRecord>? _avroDeserializer;

    public AvroCompositeKeyDeserializer(ConfluentSchemaRegistry.ISchemaRegistryClient client, string subject)
    {
        _client = client;
        _subject = subject;
    }

    public object Deserialize(ReadOnlySpan<byte> data, bool isNull, SerializationContext context)
    {
        if (isNull || data.IsEmpty)
            return new Dictionary<string, object>();

        _avroDeserializer ??= AvroSerializerFactory.GetAvroDeserializer(_subject, _client);

        var genericRecord = _avroDeserializer.DeserializeAsync(data.ToArray(), isNull, context)
            .ConfigureAwait(false).GetAwaiter().GetResult();

        if (genericRecord == null)
            return new Dictionary<string, object>();

        return ConvertFromGenericRecord(genericRecord);
    }

    private Dictionary<string, object> ConvertFromGenericRecord(GenericRecord record)
    {
        var result = new Dictionary<string, object>();
        
        if (record.Schema is RecordSchema recordSchema)
        {
            foreach (var field in recordSchema.Fields)
            {
                var value = record[field.Name];
                if (value != null)
                {
                    result[field.Name] = value;
                }
            }
        }
        
        return result;
    }
}

// Value Serializers (unchanged)
internal class AvroValueSerializer<T> : ISerializer<object> where T : class
{
    private readonly ConfluentSchemaRegistry.ISchemaRegistryClient _client;
    private readonly ConfluentSchemaRegistry.Serdes.AvroSerializer<T>? _serializer;

    public AvroValueSerializer(ConfluentSchemaRegistry.ISchemaRegistryClient client)
    {
        _client = client;
        try
        {
            _serializer = new ConfluentSchemaRegistry.Serdes.AvroSerializer<T>(client);
        }
        catch (InvalidOperationException)
        {
            _serializer = null;
        }
    }

    public byte[] Serialize(object data, SerializationContext context)
    {
        if (data is T typedData)
        {
            if (_serializer != null)
            {
                try
                {
                    return _serializer.SerializeAsync(typedData, context)
                        .ConfigureAwait(false).GetAwaiter().GetResult();
                }
                catch (InvalidOperationException)
                {
                    // Fallback to JSON if Confluent serializer rejects the type.
                }
            }

            return System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(typedData);
        }
        throw new InvalidOperationException($"Expected type {typeof(T).Name}");
    }
}

internal class AvroValueDeserializer<T> : IDeserializer<object> where T : class
{
    private readonly ConfluentSchemaRegistry.ISchemaRegistryClient _client;
    private readonly ConfluentSchemaRegistry.Serdes.AvroDeserializer<T>? _deserializer;

    public AvroValueDeserializer(ConfluentSchemaRegistry.ISchemaRegistryClient client)
    {
        _client = client;
        try
        {
            _deserializer = new ConfluentSchemaRegistry.Serdes.AvroDeserializer<T>(client);
        }
        catch (InvalidOperationException)
        {
            _deserializer = null;
        }
    }

    public object Deserialize(ReadOnlySpan<byte> data, bool isNull, SerializationContext context)
    {
        if (isNull || data.IsEmpty)
        {
            return Activator.CreateInstance<T>()!;
        }

        if (_deserializer != null)
        {
            try
            {
                return _deserializer.DeserializeAsync(data.ToArray(), isNull, context)
                    .ConfigureAwait(false).GetAwaiter().GetResult()!
                    ?? throw new InvalidOperationException("Deserialization returned null");
            }
            catch (InvalidOperationException)
            {
                // Fallback to JSON if Confluent deserializer rejects the type.
            }
        }

        return System.Text.Json.JsonSerializer.Deserialize<T>(data)!
            ?? throw new InvalidOperationException("Deserialization returned null");
    }
}
