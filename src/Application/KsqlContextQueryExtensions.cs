using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Query.Schema;
using Kafka.Ksql.Linq.Core.Models;
using Kafka.Ksql.Linq.Core.Extensions;
using Kafka.Ksql.Linq.SchemaRegistryTools;
using Confluent.SchemaRegistry;
using Kafka.Ksql.Linq.Mapping;
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
        return GetQuerySchema(context, typeof(T));
    }

    /// <summary>
    /// EntityModelからQuerySchemaを抽出（非ジェネリック版）
    /// </summary>
    public static QuerySchema? GetQuerySchema(this KsqlContext context, Type pocoType)
    {
        var entityModels = context.GetEntityModels();
        if (!entityModels.TryGetValue(pocoType, out var entityModel))
            return null;

        var schema = ExtractQuerySchemaFromEntityModel(entityModel);
        if (schema != null)
        {
            context.GetMappingRegistry().Register(
                entityModel.EntityType,
                schema.KeyProperties,
                schema.ValueProperties,
                entityModel.GetTopicName());
            return schema;
        }

        if (entityModel.AccessMode == EntityAccessMode.ReadOnly)
        {
            var client = context.GetSchemaRegistryClient();
            var meta = SchemaRegistryMetaProvider.GetMetaFromSchemaRegistry(entityModel, client);
            context.GetMappingRegistry().RegisterMeta(pocoType, meta, entityModel.GetTopicName());
            schema = new QuerySchema
            {
                SourceType = pocoType,
                TargetType = pocoType,
                TopicName = entityModel.TopicName ?? pocoType.Name.ToLowerInvariant(),
                IsValid = true,
                KeyProperties = meta.KeyProperties,
                ValueProperties = meta.ValueProperties
            };
            var ns = pocoType.Namespace?.ToLower() ?? string.Empty;
            var baseName = entityModel.GetTopicName();
            schema.KeyInfo.ClassName = $"{baseName}-key";
            schema.KeyInfo.Namespace = ns;
            schema.ValueInfo.ClassName = $"{baseName}-value";
            schema.ValueInfo.Namespace = ns;
            return schema;
        }

        return null;
    }

    /// <summary>
    /// 全てのQuerySchemaを取得
    /// </summary>
    public static Dictionary<Type, QuerySchema> GetAllQuerySchemas(this KsqlContext context)
    {
        var result = new Dictionary<Type, QuerySchema>();
        var entityModels = context.GetEntityModels();

        foreach (var (type, _) in entityModels)
        {
            var schema = context.GetQuerySchema(type);
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
        var mapping = context.GetMappingRegistry();

        foreach (var (_, schema) in querySchemas)
        {
            if (schema.IsValid)
            {
                QuerySchemaHelper.ValidateQuerySchema(schema, out _);
                mapping.Register(
                    schema.TargetType,
                    schema.KeyProperties,
                    schema.ValueProperties,
                    schema.TopicName);
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
            schema.KeyProperties = entityModel.KeyProperties
                .Take(keyCount)
                .Select(p => PropertyMeta.FromProperty(p))
                .ToArray();
            schema.ValueProperties = entityModel.AllProperties
                .Select(p => PropertyMeta.FromProperty(p))
                .ToArray();

            return schema;
        }
        catch
        {
            return null;
        }
    }

}
