using System;
using System.Collections.Generic;
using System.Reflection;

namespace Kafka.Ksql.Linq.Query.Schema;

/// <summary>
/// クエリによって生成されるKey/Value構造の定義
/// </summary>
public class QuerySchema
{
    public Type SourceType { get; set; } = default!;
    public Type TargetType { get; set; } = default!;
    public PropertyInfo[] KeyProperties { get; set; } = Array.Empty<PropertyInfo>();
    public PropertyInfo[] ValueProperties { get; set; } = Array.Empty<PropertyInfo>();
    public string TopicName { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();

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