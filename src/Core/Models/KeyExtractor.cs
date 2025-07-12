using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Core.Extensions;
using Kafka.Ksql.Linq.Serialization.Abstractions;
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
    internal static object ExtractKeyValue<T>(T entity, EntityModel entityModel) where T : class
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        if (entityModel?.KeyProperties == null || entityModel.KeyProperties.Length == 0)
            return Guid.NewGuid();

        if (entityModel.KeyProperties.Length == 1)
        {
            var keyProperty = entityModel.KeyProperties[0];
            return keyProperty.GetValue(entity) ?? string.Empty;
        }

        // 複合キー
        var keyValues = new Dictionary<string, object>();
        foreach (var property in entityModel.GetOrderedKeyProperties())
        {
            var value = property.GetValue(entity);
            keyValues[property.Name] = value ?? string.Empty;
        }

        return keyValues;
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
