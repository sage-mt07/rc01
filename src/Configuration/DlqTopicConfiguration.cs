using Kafka.Ksql.Linq.Core.Attributes;
using System;
using System.Collections.Generic;

namespace Kafka.Ksql.Linq.Configuration;

public class DlqTopicConfiguration
{
    /// <summary>
    /// DLQデータ保持時間（ミリ秒）
    /// デフォルト: 5000（5秒） - 一時退避先として機能
    /// </summary>
    [DefaultValue(5000)]
    public long RetentionMs { get; set; } = 5000;

    /// <summary>
    /// DLQトピックパーティション数
    /// デフォルト: 1（可観測性目的、パフォーマンス重視ではない）
    /// </summary>
    [DefaultValue(1)]
    public int NumPartitions { get; set; } = 1;

    /// <summary>
    /// DLQトピックレプリケーション係数
    /// デフォルト: 1（単一ブローカー環境対応）
    /// </summary>
    [DefaultValue(1)]
    public short ReplicationFactor { get; set; } = 1;

    /// <summary>
    /// DLQトピック自動作成を有効にするか
    /// デフォルト: true（Fail-Fast防止）
    /// </summary>
    [DefaultValue(true)]
    public bool EnableAutoCreation { get; set; } = true;

    /// <summary>
    /// DLQトピック追加設定
    /// 例: cleanup.policy, segment.ms, max.message.bytes等
    /// </summary>
    public Dictionary<string, string> AdditionalConfigs { get; set; } = new()
    {
        ["cleanup.policy"] = "delete",
        ["segment.ms"] = "3600000", // 1時間
        ["max.message.bytes"] = "1048576", // 1MB
        ["confluent.topic.description"] = "DLQ topic for error message handling - auto created by KafkaContext"
    };

    /// <summary>
    /// DLQ設定検証
    /// </summary>
    public void Validate()
    {
        if (RetentionMs <= 0)
            throw new ArgumentException("DLQ RetentionMs must be positive", nameof(RetentionMs));

        if (NumPartitions <= 0)
            throw new ArgumentException("DLQ NumPartitions must be positive", nameof(NumPartitions));

        if (ReplicationFactor <= 0)
            throw new ArgumentException("DLQ ReplicationFactor must be positive", nameof(ReplicationFactor));
    }

    /// <summary>
    /// 設定サマリー取得（デバッグ用）
    /// </summary>
    public string GetSummary()
    {
        return $"DLQ Config: Retention={RetentionMs}ms, Partitions={NumPartitions}, " +
               $"Replicas={ReplicationFactor}, AutoCreate={EnableAutoCreation}";
    }
}
public static class DlqConfigurationExtensions
{
    /// <summary>
    /// DLQ設定をカスタマイズ
    /// </summary>
    public static KsqlDslOptions ConfigureDlq(
        this KsqlDslOptions options,
        Action<DlqTopicConfiguration> configureDlq)
    {
        configureDlq?.Invoke(options.DlqConfiguration);
        options.DlqConfiguration.Validate();
        return options;
    }

    /// <summary>
    /// DLQ保持時間設定（利便性メソッド）
    /// </summary>
    public static KsqlDslOptions WithDlqRetention(
        this KsqlDslOptions options,
        TimeSpan retention)
    {
        options.DlqConfiguration.RetentionMs = (long)retention.TotalMilliseconds;
        return options;
    }

    /// <summary>
    /// DLQ無効化（テスト環境等）
    /// </summary>
    public static KsqlDslOptions DisableDlqAutoCreation(this KsqlDslOptions options)
    {
        options.DlqConfiguration.EnableAutoCreation = false;
        return options;
    }
}
