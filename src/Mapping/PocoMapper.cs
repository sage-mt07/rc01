using Kafka.Ksql.Linq.Query.Schema;
using Kafka.Ksql.Linq.Core.Models;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Kafka.Ksql.Linq.Mapping;

/// <summary>
/// Utility for converting between POCO instances and key/value pairs
/// based on <see cref="QuerySchema"/> metadata.
/// </summary>
public static class PocoMapper
{
    public static (object Key, TEntity Value) ToKeyValue<TEntity>(TEntity entity, QuerySchema schema) where TEntity : class
    {
        if (entity == null) throw new ArgumentNullException(nameof(entity));
        if (schema == null) throw new ArgumentNullException(nameof(schema));

        var parts = ExtractKeyParts(entity, schema);
        var key = KeyExtractor.BuildTypedKey(parts);

        if (key != null && key is not Dictionary<string, object> &&
            !KeyExtractor.IsSupportedKeyType(key.GetType()))
        {
            throw new NotSupportedException($"Key type {key.GetType().Name} is not supported.");
        }

        return (key!, entity);
    }

    public static TEntity FromKeyValue<TEntity>(object? key, TEntity valueEntity, QuerySchema schema) where TEntity : class
    {
        if (valueEntity == null) throw new ArgumentNullException(nameof(valueEntity));
        if (schema == null) throw new ArgumentNullException(nameof(schema));

        if (schema.KeyProperties.Length == 0 || key == null)
            return valueEntity;

        if (schema.KeyProperties.Length == 1)
        {
            MergeSingleKey(key, valueEntity, schema.KeyProperties[0]);
            return valueEntity;
        }

        MergeCompositeKey(key, valueEntity, schema.KeyProperties);
        return valueEntity;
    }

    private static List<CompositeKeyPart> ExtractKeyParts<TEntity>(TEntity entity, QuerySchema schema) where TEntity : class
    {
        var parts = new List<CompositeKeyPart>();

        foreach (var property in schema.KeyProperties)
        {
            var valueObj = property.GetValue(entity);
            var valueStr = valueObj?.ToString() ?? string.Empty;
            parts.Add(new CompositeKeyPart(property.Name, property.PropertyType, valueStr));
        }
        return parts;
    }

    private static void MergeSingleKey(object keyValue, object target, PropertyInfo property)
    {
        var converted = ConvertKeyValue(keyValue, property.PropertyType);
        property.SetValue(target, converted);
    }

    private static void MergeCompositeKey(object keyValue, object target, PropertyInfo[] properties)
    {
        if (keyValue is not Dictionary<string, object> dict)
            throw new InvalidOperationException($"Expected Dictionary<string, object> for composite key, but got {keyValue.GetType().Name}");

        foreach (var property in properties)
        {
            if (dict.TryGetValue(property.Name, out var value))
            {
                var converted = ConvertKeyValue(value, property.PropertyType);
                property.SetValue(target, converted);
            }
        }
    }

    private static object? ConvertKeyValue(object? value, Type targetType)
    {
        if (value == null)
        {
            if (targetType.IsClass || Nullable.GetUnderlyingType(targetType) != null)
                return null;
            return Activator.CreateInstance(targetType);
        }

        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (value.GetType() == underlyingType)
            return value;

        if (underlyingType == typeof(Guid))
        {
            return value switch
            {
                string str => Guid.Parse(str),
                byte[] bytes => new Guid(bytes),
                Guid g => g,
                _ => throw new InvalidCastException($"Cannot convert {value.GetType().Name} to Guid")
            };
        }

        if (underlyingType == typeof(byte[]))
        {
            return value switch
            {
                byte[] bytes => bytes,
                string str => Convert.FromBase64String(str),
                _ => throw new InvalidCastException($"Cannot convert {value.GetType().Name} to byte[]")
            };
        }

        return Convert.ChangeType(value, underlyingType);
    }
}
