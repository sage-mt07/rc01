using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Core.Attributes;
using Kafka.Ksql.Linq.Messaging.Configuration;
using System.Collections.Generic;

namespace Kafka.Ksql.Linq.Configuration;
public class KsqlDslOptions
{
    /// <summary>
    /// バリデーションモード（ルートレベル配置）
    /// </summary>
    public ValidationMode ValidationMode { get; init; } = ValidationMode.Strict;

    /// <summary>
    /// 共通設定（BootstrapServers, ClientId等）
    /// </summary>
    public CommonSection Common { get; init; } = new();

    /// <summary>
    /// トピック別設定（Producer/Consumer設定をトピック単位で管理）
    /// </summary>
    public Dictionary<string, TopicSection> Topics { get; init; } = new();

    /// <summary>
    /// Schema Registry設定
    /// </summary>
    public SchemaRegistrySection SchemaRegistry { get; init; } = new();

    public List<EntityConfiguration> Entities { get; init; } = new();

    [DefaultValue("dead.letter.queue")]
    public string DlqTopicName { get; set; } = "dead.letter.queue";

    public DlqTopicConfiguration DlqConfiguration { get; init; } = new();

    /// <summary>
    /// デシリアライズ失敗時のポリシー
    /// </summary>
    public DeserializationErrorPolicy DeserializationErrorPolicy { get; set; } = DeserializationErrorPolicy.Skip;

    /// <summary>
    /// Finalトピックからの読み取りをデフォルトで有効にするか
    /// </summary>
    public bool ReadFromFinalTopicByDefault { get; set; } = false;
}
