using System;
using System.Collections.Generic;
using System.Reflection;

namespace Kafka.Ksql.Linq.Query.Schema;

/// <summary>
/// クエリによって生成されるKey/Value構造の定義
/// </summary>
public class KeyValueSchemaInfo
{
    public string ClassName { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public PropertyInfo[] Properties { get; set; } = Array.Empty<PropertyInfo>();
    public string SchemaVersion { get; set; } = "1";
    public string Compatibility { get; set; } = string.Empty;
}

public class QuerySchema
{
    public Type SourceType { get; set; } = default!;
    public Type TargetType { get; set; } = default!;
    public KeyValueSchemaInfo KeyInfo { get; set; } = new();
    public KeyValueSchemaInfo ValueInfo { get; set; } = new();
    public string TopicName { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();

    public PropertyInfo[] KeyProperties
    {
        get => KeyInfo.Properties;
        set => KeyInfo.Properties = value;
    }

    public PropertyInfo[] ValueProperties
    {
        get => ValueInfo.Properties;
        set => ValueInfo.Properties = value;
    }

    /// <summary>
    /// 単一キーかどうか
    /// </summary>
    public bool IsSingleKey => KeyProperties.Length == 1;

    /// <summary>
    /// 複合キーかどうか
    /// </summary>
    public bool IsCompositeKey => KeyProperties.Length > 1;

    /// <summary>
    /// キーレス（Stream型）かどうか
    /// </summary>
    public bool IsKeyless => KeyProperties.Length == 0;

    /// <summary>
    /// Key型を取得
    /// </summary>
    public Type GetKeyType()
    {
        if (IsKeyless) return typeof(string);
        if (IsSingleKey) return KeyProperties[0].PropertyType;
        return typeof(Dictionary<string, object>); // 複合キー
    }

    /// <summary>
    /// Value型を取得（常にTargetType）
    /// </summary>
    public Type GetValueType() => TargetType;

    /// <summary>
    /// Stream/Table判定
    /// </summary>
    public string GetStreamTableType() => IsKeyless ? "Stream" : "Table";
}

/// <summary>
/// クエリスキーマ解析結果
/// </summary>
public class QuerySchemaResult
{
    public QuerySchema? Schema { get; set; }
    public bool Success { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public List<string> Warnings { get; set; } = new();

    public static QuerySchemaResult Failure(string error) => 
        new() { Success = false, ErrorMessage = error };

    public static QuerySchemaResult CreateSuccess(QuerySchema schema) => 
        new() { Success = true, Schema = schema };
}