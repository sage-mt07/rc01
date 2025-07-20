using Kafka.Ksql.Linq.Core.Models;
using Kafka.Ksql.Linq.Query.Pipeline;
using System;
using System.Collections.Generic;

namespace Kafka.Ksql.Linq.Query.Schema;

public class QuerySchema
{
    public Type SourceType { get; set; } = default!;
    public Type TargetType { get; set; } = default!;
    public KeyValueSchemaInfo KeyInfo { get; set; } = new();
    public KeyValueSchemaInfo ValueInfo { get; set; } = new();
    public string TopicName { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    /// <summary>
    /// Query execution mode. <see cref="QueryExecutionMode.Unspecified"/> if not explicitly configured.
    /// </summary>
    public QueryExecutionMode ExecutionMode { get; set; } = QueryExecutionMode.Unspecified;

    /// <summary>
    /// Indicates if MIN/MAX or other stream-only aggregate functions are used.
    /// </summary>
    public bool UsesStreamOnlyAggregates { get; set; }

    public PropertyMeta[] KeyProperties
    {
        get => KeyInfo.Properties;
        set => KeyInfo.Properties = value;
    }

    public PropertyMeta[] ValueProperties
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

