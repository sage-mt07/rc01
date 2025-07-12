using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Serialization.Avro.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace Kafka.Ksql.Linq.Serialization.Avro.Management;
internal class AvroSchemaBuilder : IAvroSchemaProvider
{
    public Task<string> GetKeySchemaAsync<T>() where T : class
    {
        var keySchema = GenerateKeySchema<T>();
        return Task.FromResult(keySchema);
    }

    public Task<string> GetValueSchemaAsync<T>() where T : class
    {
        var valueSchema = GenerateValueSchema<T>();
        return Task.FromResult(valueSchema);
    }

    public Task<(string keySchema, string valueSchema)> GetSchemasAsync<T>() where T : class
    {
        var keySchema = GenerateKeySchema<T>();
        var valueSchema = GenerateValueSchema<T>();
        return Task.FromResult((keySchema, valueSchema));
    }

    public Task<bool> ValidateSchemaAsync(string schema)
    {
        var isValid = ValidateAvroSchema(schema);
        return Task.FromResult(isValid);
    }

    public string GenerateKeySchema<T>() where T : class
    {
        var entityType = typeof(T);
        var keyProperties = GetKeyProperties(entityType);

        if (keyProperties.Length == 0)
            return GeneratePrimitiveSchema(typeof(string));

        if (keyProperties.Length == 1)
            return GeneratePrimitiveSchema(keyProperties[0].PropertyType);

        return GenerateCompositeKeySchema(keyProperties);
    }

    public string GenerateValueSchema<T>() where T : class
    {
        var entityType = typeof(T);

        var schema = new AvroSchema
        {
            Type = "record",
            // Name should correspond to the entity type for Schema Registry compatibility
            Name = entityType.Name,
            Namespace = $"{entityType.Namespace}.Avro",
            Fields = GenerateFieldsFromProperties(GetSchemaProperties(entityType))
        };

        return SerializeSchema(schema);
    }

    private string GeneratePrimitiveSchema(Type primitiveType)
    {
        var underlyingType = Nullable.GetUnderlyingType(primitiveType) ?? primitiveType;

        if (underlyingType == typeof(decimal))
        {
            return JsonSerializer.Serialize(new
            {
                type = "bytes",
                logicalType = "decimal",
                precision = 18,
                scale = 4
            });
        }

        if (underlyingType == typeof(DateTime) || underlyingType == typeof(DateTimeOffset))
        {
            return JsonSerializer.Serialize(new
            {
                type = "long",
                logicalType = "timestamp-millis"
            });
        }

        if (underlyingType == typeof(Guid))
        {
            return JsonSerializer.Serialize(new
            {
                type = "string",
                logicalType = "uuid"
            });
        }

        return underlyingType switch
        {
            Type t when t == typeof(string) => "\"string\"",
            Type t when t == typeof(int) => "\"int\"",
            Type t when t == typeof(long) => "\"long\"",
            Type t when t == typeof(byte[]) => "\"bytes\"",
            _ => "\"string\""
        };
    }

    private string GenerateCompositeKeySchema(PropertyInfo[] keyProperties)
    {
        var fields = new List<AvroField>();

        foreach (var prop in keyProperties)
        {
            fields.Add(new AvroField
            {
                Name = prop.Name,
                Type = MapPropertyToAvroType(prop)
            });
        }

        var schema = new AvroSchema
        {
            Type = "record",
            Name = "CompositeKey",
            Fields = fields
        };

        return SerializeSchema(schema);
    }

    private List<AvroField> GenerateFieldsFromProperties(PropertyInfo[] properties)
    {
        var fields = new List<AvroField>();

        foreach (var property in properties)
        {

            fields.Add(new AvroField
            {
                Name = property.Name,
                Type = MapPropertyToAvroType(property)
            });
        }

        return fields;
    }

    private object MapPropertyToAvroType(PropertyInfo property)
    {
        var propertyType = property.PropertyType;
        var underlyingType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        var isNullable = Nullable.GetUnderlyingType(propertyType) != null ||
                       (!propertyType.IsValueType && IsNullableReferenceType(property));

        var avroType = GetBasicAvroType(property, underlyingType);

        return isNullable ? new object[] { "null", avroType } : avroType;
    }

    private object GetBasicAvroType(PropertyInfo property, Type underlyingType)
    {
        if (underlyingType == typeof(decimal))
        {
            return new
            {
                type = "bytes",
                logicalType = "decimal",
                precision = 18,
                scale = 4
            };
        }

        if (underlyingType == typeof(DateTime) || underlyingType == typeof(DateTimeOffset))
        {
            return new
            {
                type = "long",
                logicalType = "timestamp-millis"
            };
        }

        if (underlyingType == typeof(Guid))
        {
            return new
            {
                type = "string",
                logicalType = "uuid"
            };
        }

        return underlyingType switch
        {
            Type t when t == typeof(bool) => "boolean",
            Type t when t == typeof(int) => "int",
            Type t when t == typeof(long) => "long",
            Type t when t == typeof(float) => "float",
            Type t when t == typeof(double) => "double",
            Type t when t == typeof(string) => "string",
            Type t when t == typeof(byte[]) => "bytes",
            _ => "string"
        };
    }

    private bool IsNullableReferenceType(PropertyInfo property)
    {
        try
        {
            var nullabilityContext = new NullabilityInfoContext();
            var nullabilityInfo = nullabilityContext.Create(property);
            return nullabilityInfo.WriteState == NullabilityState.Nullable;
        }
        catch
        {
            return !property.PropertyType.IsValueType;
        }
    }

    private PropertyInfo[] GetKeyProperties(Type entityType)
    {
        var allProperties = entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        return Array.Empty<PropertyInfo>();
    }

    private PropertyInfo[] GetSchemaProperties(Type entityType)
    {
        return entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
    }

    private string GetTopicName(Type entityType)
    {
        return entityType.Name.ToLowerInvariant();
    }

    private string SerializeSchema(AvroSchema schema)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        return JsonSerializer.Serialize(schema, options);
    }

    private bool ValidateAvroSchema(string schema)
    {
        if (string.IsNullOrEmpty(schema))
            return false;

        try
        {
            using var document = JsonDocument.Parse(schema);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Object)
            {
                if (!root.TryGetProperty("type", out var typeElement))
                    return false;

                var typeValue = typeElement.GetString();

                if (typeValue == "record")
                {
                    if (!root.TryGetProperty("name", out var nameElement))
                        return false;

                    var nameValue = nameElement.GetString();
                    if (string.IsNullOrEmpty(nameValue))
                        return false;
                }

                return true;
            }

            if (root.ValueKind == JsonValueKind.String)
            {
                var primitiveType = root.GetString();
                return !string.IsNullOrEmpty(primitiveType);
            }

            if (root.ValueKind == JsonValueKind.Array)
            {
                return root.GetArrayLength() > 0;
            }

            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
