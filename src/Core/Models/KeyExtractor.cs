using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Core.Extensions;
using Kafka.Ksql.Linq.Serialization.Abstractions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Kafka.Ksql.Linq.Core.Models;

internal static class KeyExtractor
{
    /// <summary>
    /// エンティティモデルから複合キーかどうかを判定
    /// </summary>
    internal static bool IsCompositeKey(EntityModel entityModel)
    {
        return entityModel?.KeyProperties != null && entityModel.KeyProperties.Length > 1;
    }

    /// <summary>
    /// エンティティモデルからキー型を決定
    /// </summary>
    internal static Type DetermineKeyType(EntityModel entityModel)
    {
        if (entityModel?.KeyProperties == null || entityModel.KeyProperties.Length == 0)
            return typeof(string);

        if (entityModel.KeyProperties.Length == 1)
            return entityModel.KeyProperties[0].PropertyType;

        // 複合キーの場合
        return typeof(Dictionary<string, object>);
    }

    /// <summary>
    /// エンティティ型からキープロパティを抽出
    /// </summary>
    internal static PropertyInfo[] ExtractKeyProperties(Type entityType)
    {
        return Array.Empty<PropertyInfo>();
    }

    /// <summary>
    /// エンティティインスタンスから複合キー要素を抽出（新方式）
    /// </summary>
    internal static List<CompositeKeyPart> ExtractKeyParts<T>(T entity, EntityModel entityModel) where T : class
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        var parts = new List<CompositeKeyPart>();

        if (entityModel?.KeyProperties == null || entityModel.KeyProperties.Length == 0)
            return parts;

        foreach (var property in entityModel.GetOrderedKeyProperties())
        {
            var valueObj = property.GetValue(entity);
            var valueStr = valueObj?.ToString() ?? string.Empty;
            parts.Add(new CompositeKeyPart(property.Name, property.PropertyType, valueStr));
        }

        return parts;
    }

    private static object? ConvertStringToType(string value, Type type, ILogger? logger)
    {
        try
        {
            var underlying = Nullable.GetUnderlyingType(type) ?? type;

            if (underlying == typeof(string))
                return value;
            if (underlying == typeof(int))
                return string.IsNullOrEmpty(value) ? 0 : int.Parse(value);
            if (underlying == typeof(long))
                return string.IsNullOrEmpty(value) ? 0L : long.Parse(value);
            if (underlying == typeof(Guid))
                return string.IsNullOrEmpty(value) ? Guid.Empty : Guid.Parse(value);

            return Convert.ChangeType(value, underlying);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to convert key value '{Value}' to {Type}", value, type.Name);
            throw;
        }
    }

    internal static object BuildTypedKey(IList<CompositeKeyPart> parts, ILogger? logger = null)
    {
        if (parts.Count == 0)
            return Guid.NewGuid();

        if (parts.Count == 1)
        {
            return ConvertStringToType(parts[0].Value, parts[0].KeyType, logger) ?? string.Empty;
        }

        var dict = new Dictionary<string, object>();
        foreach (var part in parts)
        {
            dict[part.KeyName] = ConvertStringToType(part.Value, part.KeyType, logger) ?? string.Empty;
        }
        return dict;
    }

    /// <summary>
    /// 定義順に基づき先頭 count 個のプロパティを抽出
    /// </summary>
    internal static PropertyInfo[] ExtractKeyProperties(Type entityType, int count)
    {
        if (count <= 0)
            return Array.Empty<PropertyInfo>();

        return entityType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .OrderBy(p => p.MetadataToken)
            .Take(count)
            .ToArray();
    }

    /// <summary>
    /// エンティティインスタンスからキー値を抽出
    /// </summary>
    internal static object ExtractKeyValue<T>(T entity, EntityModel entityModel, ILogger? logger = null) where T : class
    {
        var parts = ExtractKeyParts(entity, entityModel);
        return BuildTypedKey(parts, logger);
    }

    /// <summary>
    /// キー値を文字列に変換
    /// </summary>
    internal static string KeyToString(object keyValue)
    {
        if (keyValue == null)
            return string.Empty;

        if (keyValue is string str)
            return str;

        if (keyValue is Dictionary<string, object> dict)
        {
            var keyPairs = dict.Select(kvp => $"{kvp.Key}={kvp.Value}");
            return string.Join("|", keyPairs);
        }

        if (keyValue is IList<CompositeKeyPart> parts)
        {
            var keyPairs = parts.Select(p => $"{p.KeyName}={p.Value}");
            return string.Join("|", keyPairs);
        }

        return keyValue.ToString() ?? string.Empty;
    }

    /// <summary>
    /// キー型がサポートされているかチェック
    /// </summary>
    internal static bool IsSupportedKeyType(Type keyType)
    {
        var underlyingType = Nullable.GetUnderlyingType(keyType) ?? keyType;

        return underlyingType == typeof(string) ||
               underlyingType == typeof(int) ||
               underlyingType == typeof(long) ||
               underlyingType == typeof(Guid);
    }

    /// <summary>
    /// EntityModelからAvroEntityConfigurationを作成
    /// </summary>
    internal static AvroEntityConfiguration ToAvroEntityConfiguration(EntityModel entityModel)
    {
        if (entityModel == null)
            throw new ArgumentNullException(nameof(entityModel));

        var config = new AvroEntityConfiguration(entityModel.EntityType)
        {
            TopicName = entityModel.TopicName,
            KeyProperties = entityModel.KeyProperties
        };

        return config;
    }
}
