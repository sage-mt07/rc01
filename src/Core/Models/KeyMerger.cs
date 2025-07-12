using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Core.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Kafka.Ksql.Linq.Core.Models;

internal static class KeyMerger
{
    /// <summary>
    /// 受信したKey/ValueからPOCOを再構築
    /// </summary>
    /// <typeparam name="T">エンティティ型</typeparam>
    /// <param name="keyValue">デシリアライズされたキー値</param>
    /// <param name="valueEntity">デシリアライズされたValueエンティティ</param>
    /// <param name="entityModel">エンティティモデル</param>
    /// <returns>キー値が復元された完全なPOCO</returns>
    internal static T MergeKeyValue<T>(object? keyValue, T valueEntity, EntityModel entityModel) where T : class
    {
        if (valueEntity == null)
            throw new ArgumentNullException(nameof(valueEntity));
        if (entityModel == null)
            throw new ArgumentNullException(nameof(entityModel));

        // キープロパティがない場合はvalueEntityをそのまま返す
        if (entityModel.KeyProperties == null || entityModel.KeyProperties.Length == 0)
            return valueEntity;

        // キー値がnullの場合も valueEntity をそのまま返す
        if (keyValue == null)
            return valueEntity;

        try
        {
            if (entityModel.KeyProperties.Length == 1)
            {
                // 単一キーの場合
                MergeSingleKey(keyValue, valueEntity, entityModel.KeyProperties[0]);
            }
            else
            {
                // 複合キーの場合
                MergeCompositeKey(keyValue, valueEntity, entityModel.KeyProperties);
            }

            return valueEntity;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to merge key value into entity {typeof(T).Name}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 単一キーの結合
    /// </summary>
    private static void MergeSingleKey(object keyValue, object valueEntity, PropertyInfo keyProperty)
    {
        try
        {
            var convertedValue = ConvertKeyValue(keyValue, keyProperty.PropertyType);
            keyProperty.SetValue(valueEntity, convertedValue);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to set single key property {keyProperty.Name}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 複合キーの結合
    /// </summary>
    private static void MergeCompositeKey(object keyValue, object valueEntity, PropertyInfo[] keyProperties)
    {
        if (keyValue is not Dictionary<string, object> keyDict)
        {
            throw new InvalidOperationException(
                $"Expected Dictionary<string, object> for composite key, but got {keyValue.GetType().Name}");
        }

        foreach (var property in keyProperties)
        {
            if (keyDict.TryGetValue(property.Name, out var value))
            {
                try
                {
                    var convertedValue = ConvertKeyValue(value, property.PropertyType);
                    property.SetValue(valueEntity, convertedValue);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Failed to set composite key property {property.Name}: {ex.Message}", ex);
                }
            }
        }
    }

    /// <summary>
    /// キー値を目標の型に変換
    /// </summary>
    private static object? ConvertKeyValue(object? value, Type targetType)
    {
        if (value == null)
        {
            // Nullable型またはreference型の場合はnullを許可
            if (targetType.IsClass || Nullable.GetUnderlyingType(targetType) != null)
                return null;

            // Value型の場合はdefault値を返す
            return Activator.CreateInstance(targetType);
        }

        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        // 既に同じ型の場合はそのまま返す
        if (value.GetType() == underlyingType)
            return value;

        // 型変換を試行
        try
        {
            // Guid特別処理
            if (underlyingType == typeof(Guid))
            {
                return value switch
                {
                    string str => Guid.Parse(str),
                    byte[] bytes => new Guid(bytes),
                    Guid guid => guid,
                    _ => throw new InvalidCastException($"Cannot convert {value.GetType().Name} to Guid")
                };
            }

            // byte[]特別処理
            if (underlyingType == typeof(byte[]))
            {
                return value switch
                {
                    byte[] bytes => bytes,
                    string str => Convert.FromBase64String(str),
                    _ => throw new InvalidCastException($"Cannot convert {value.GetType().Name} to byte[]")
                };
            }

            // 一般的な型変換
            return Convert.ChangeType(value, underlyingType);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Cannot convert value '{value}' ({value.GetType().Name}) to type {targetType.Name}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// エンティティからキー値を抽出して検証（デバッグ用）
    /// </summary>
    internal static object? ExtractCurrentKeyValue<T>(T entity, EntityModel entityModel) where T : class
    {
        return KeyExtractor.ExtractKeyValue(entity, entityModel);
    }

    /// <summary>
    /// Key/Value結合前後のキー値の整合性を検証（テスト用）
    /// </summary>
    internal static bool ValidateKeyConsistency<T>(
        object? originalKey,
        T mergedEntity,
        EntityModel entityModel) where T : class
    {
        try
        {
            var extractedKey = ExtractCurrentKeyValue(mergedEntity, entityModel);
            return KeyValuesEqual(originalKey, extractedKey);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// キー値の等価性チェック
    /// </summary>
    private static bool KeyValuesEqual(object? key1, object? key2)
    {
        if (key1 == null && key2 == null) return true;
        if (key1 == null || key2 == null) return false;

        // Dictionary比較（複合キー）
        if (key1 is Dictionary<string, object> dict1 && key2 is Dictionary<string, object> dict2)
        {
            if (dict1.Count != dict2.Count) return false;

            return dict1.All(kvp =>
                dict2.TryGetValue(kvp.Key, out var value) &&
                Equals(kvp.Value, value));
        }

        // 通常の等価性比較
        return Equals(key1, key2);
    }

    /// <summary>
    /// エンティティ型がKey/Value結合をサポートしているかチェック
    /// </summary>
    internal static bool SupportsKeyMerging(Type entityType)
    {
        if (!entityType.IsClass || entityType.IsAbstract)
            return false;

        // Kafkaエンティティかどうかチェック
        return entityType.IsKafkaEntity();
    }


}
