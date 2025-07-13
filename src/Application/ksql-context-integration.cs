using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Query.Schema;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Kafka.Ksql.Linq.Application;

/// <summary>
/// KsqlContextにクエリスキーマ管理機能を追加
/// </summary>
public static class KsqlContextQueryExtensions
{
    /// <summary>
    /// EntityModelからQuerySchemaを抽出
    /// </summary>
    public static QuerySchema? GetQuerySchema<T>(this KsqlContext context) where T : class
    {
        var entityModels = context.GetEntityModels();
        if (!entityModels.TryGetValue(typeof(T), out var entityModel))
            return null;

        return ExtractQuerySchemaFromEntityModel(entityModel);
    }

    /// <summary>
    /// 全てのQuerySchemaを取得
    /// </summary>
    public static Dictionary<Type, QuerySchema> GetAllQuerySchemas(this KsqlContext context)
    {
        var result = new Dictionary<Type, QuerySchema>();
        var entityModels = context.GetEntityModels();

        foreach (var (type, entityModel) in entityModels)
        {
            var schema = ExtractQuerySchemaFromEntityModel(entityModel);
            if (schema != null)
            {
                result[type] = schema;
            }
        }

        return result;
    }

    /// <summary>
    /// クエリベースエンティティの自動登録
    /// </summary>
    public static void RegisterQuerySchemas(this KsqlContext context)
    {
        var querySchemas = context.GetAllQuerySchemas();

        foreach (var (_, schema) in querySchemas)
        {
            if (schema.IsValid)
            {
                // 登録処理は廃止されたため、ここでは検証のみ行う
                QuerySchemaHelper.ValidateQuerySchema(schema, out _);
            }
        }
    }

    /// <summary>
    /// EntityModelからQuerySchemaを抽出
    /// </summary>
    private static QuerySchema? ExtractQuerySchemaFromEntityModel(EntityModel entityModel)
    {
        // ValidationResult.WarningsからQuerySchema情報を復元
        var warnings = entityModel.ValidationResult?.Warnings ?? new List<string>();
        var schemaWarning = warnings.FirstOrDefault(w => w.StartsWith("QuerySchema:"));
        
        if (schemaWarning == null)
            return null;

        // 正規表現でスキーマ情報をパース
        var match = Regex.Match(schemaWarning,
            @"QuerySchema:Source=([^,]+),Target=([^,]+),KeyClass=([^,]+),KeyNs=([^,]+),ValueClass=([^,]+),ValueNs=([^,]+),Keys=(\d+),Type=([^,]+)");
        
        if (!match.Success)
            return null;

        try
        {
            var sourceTypeName = match.Groups[1].Value;
            var targetTypeName = match.Groups[2].Value;
            var keyClass = match.Groups[3].Value;
            var keyNs = match.Groups[4].Value;
            var valueClass = match.Groups[5].Value;
            var valueNs = match.Groups[6].Value;
            var keyCount = int.Parse(match.Groups[7].Value);
            var streamTableType = match.Groups[8].Value;

            var schema = new QuerySchema
            {
                SourceType = Type.GetType(sourceTypeName) ?? entityModel.EntityType,
                TargetType = entityModel.EntityType,
                TopicName = entityModel.TopicName ?? entityModel.EntityType.Name.ToLowerInvariant(),
                IsValid = entityModel.IsValid
            };
            schema.KeyInfo.ClassName = keyClass;
            schema.KeyInfo.Namespace = keyNs;
            schema.ValueInfo.ClassName = valueClass;
            schema.ValueInfo.Namespace = valueNs;
            schema.KeyProperties = entityModel.KeyProperties.Take(keyCount).ToArray();
            schema.ValueProperties = entityModel.AllProperties;

            return schema;
        }
        catch
        {
            return null;
        }
    }

}

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