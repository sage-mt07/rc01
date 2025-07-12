# Core Namespace Redesign Plan

🗕 2025年7月15日（JST）
🧐 作業者: 広夢（戦略広報AI）

## 不要となるコード一覧

以下のクラス・属性はLINQ式ベースのキー管理へ移行するため削除予定です。

- `Kafka.Ksql.Linq.Core.Abstractions.KsqlStreamAttribute`
- `Kafka.Ksql.Linq.Core.Abstractions.KsqlTableAttribute`
- `Kafka.Ksql.Linq.Core.Abstractions.TopicAttribute`
- `Kafka.Ksql.Linq.Core.Abstractions.AvroTimestampAttribute`
- `Kafka.Ksql.Linq.Core.Abstractions.DateTimeFormatAttribute`
- `Kafka.Ksql.Linq.Core.Abstractions.DecimalPrecisionAttribute`
- `Kafka.Ksql.Linq.Core.Abstractions.KafkaIgnoreAttribute`

上記の削除に伴い、`EntityModel` から属性読み取りロジックも除去予定です。

### 削除対象ソースと依存箇所

| 削除対象 | 参照ファイル例 |
|-----------|----------------|
| `KsqlStreamAttribute` | `AvroEntityConfiguration.cs`, `EntityModel.cs`, `DDLQueryGenerator.cs` |
| `KsqlTableAttribute` | `AvroEntityConfiguration.cs`, `EntityModel.cs`, `DDLQueryGenerator.cs` |
| `TopicAttribute` | `AvroEntityConfiguration.cs`, `AvroSchemaBuilder.cs`, `KafkaConsumerManager.cs` 等 |
| `AvroTimestampAttribute` | `WindowAggregatedEntitySet.cs`, `WindowedEntitySet.cs` |
| `DateTimeFormatAttribute` | `AvroSchemaBuilder.cs`, `UnifiedSchemaGenerator.cs` |
| `DecimalPrecisionAttribute` | `AvroSchemaBuilder.cs`, `UnifiedSchemaGenerator.cs` |
| `KafkaIgnoreAttribute` | `AvroEntityConfiguration.cs`, `AvroSchemaBuilder.cs`, `UnifiedSchemaGenerator.cs` |

テストでは `TopicFluentApiTests` などが `TopicAttribute` を参照しているため、削除時は更新が必要です。

## 新API / IF 設計ドラフト

```csharp
public interface IEntityBuilder<T> where T : class
{
    IEntityBuilder<T> HasKey<TKey>(Expression<Func<T, TKey>> key);
    IEntityBuilder<T> WithTopic(string name, int partitions = 1, int replication = 1);
}
```

- `HasKey` は必須呼び出しとし、複合キー指定にも対応します。
- トピック設定は `WithTopic` メソッドに集約し、属性ではなくコード構成で定義します。

## 旧APIとの差分

旧APIでは属性によりエンティティ設定を行っていました。新APIではFluentメソッドを使用します。

| 機能 | 旧API例 | 新API例 |
|------|---------|---------|
| キー指定 | `\[Key\] public int Id { get; set; }` | `builder.HasKey(e => e.Id);` |
| 複合キー | `\[KeyOrder(1)\] int A; \[KeyOrder(2)\] int B;` | `builder.HasKey(e => new { e.A, e.B });` |
| トピック名 | `\[Topic("orders")\]` | `builder.WithTopic("orders");` |

### 旧→新 1:1マッピング一覧表

| 旧Attribute | 新API | 備考 |
|-------------|-------|------|
| `TopicAttribute` | `builder.WithTopic()` | |
| `KsqlStreamAttribute` | `builder.AsStream()` | |
| `KsqlTableAttribute` | `builder.AsTable()` | |
| `DecimalPrecisionAttribute` | `builder.WithDecimalPrecision()` | |
| `AvroTimestampAttribute` | `DateTime/DateTimeOffset` プロパティを定義 | 属性は廃止 |
| `DateTimeFormatAttribute` | *(なし)* | `DateTime` 型をそのまま利用 |
| `KafkaIgnoreAttribute` | *(なし)* | 対象プロパティを定義しない |

### 削除理由と互換性メモ

- 属性ベース実装は拡張性に乏しく、LINQ式から推論可能な構造へ統一するため。
- 既存の `[Topic]` などは新APIの `WithTopic` へ置き換える必要があります。
- 互換レイヤーは提供しない方針のため、サンプルとテストは全面更新を予定しています。

## 今後の作業

1. 上記不要コードの削除PRを別途作成
2. サンプルとテストを新APIに合わせて更新
3. ドキュメント全体のリンクを整理
