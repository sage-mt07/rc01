using Kafka.Ksql.Linq.Query.Schema;
using System;
using System.Collections.Generic;

namespace Kafka.Ksql.Linq.Application;


/// <summary>
/// QuerySchemaヘルパーメソッド
/// </summary>
public static class QuerySchemaHelper
{
    /// <summary>
    /// QuerySchemaの妥当性検証
    /// </summary>
    public static bool ValidateQuerySchema(QuerySchema schema, out List<string> errors)
    {
        errors = new List<string>();

        if (schema.TargetType == null)
            errors.Add("Target type is required");

        if (string.IsNullOrEmpty(schema.TopicName))
            errors.Add("Topic name is required");

        // キー型の検証
        foreach (var keyProp in schema.KeyProperties)
        {
            if (!IsSupportedKeyType(keyProp.PropertyType))
            {
                errors.Add($"Unsupported key type: {keyProp.PropertyType.Name}");
            }
        }

        return errors.Count == 0;
    }

    /// <summary>
    /// サポートされるキー型の判定
    /// </summary>
    private static bool IsSupportedKeyType(Type type)
    {
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;
        return underlyingType == typeof(string) ||
               underlyingType == typeof(int) ||
               underlyingType == typeof(long) ||
               underlyingType == typeof(Guid);
    }

    /// <summary>
    /// QuerySchemaの概要表示
    /// </summary>
    public static string GetSchemaSummary(QuerySchema schema)
    {
        var keyInfo = schema.KeyProperties.Length == 0 ? "keyless" : 
                     $"{schema.KeyProperties.Length} key(s)";
        
        return $"{schema.TargetType.Name} → {schema.TopicName} " +
               $"({schema.GetStreamTableType()}, {keyInfo})";
    }
}
