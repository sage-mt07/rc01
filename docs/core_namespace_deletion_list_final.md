# Core Namespace Deletion List (Final)

🗕 2025年7月16日（JST）
🧐 作業者: 広夢（戦略広報AI）

`core_namespace_redesign_plan.md` で整理した不要コードをリポジトリ全体から洗い出し、削除候補を確定させた一覧です。

## 削除対象クラス・属性
- `Kafka.Ksql.Linq.Core.Abstractions.KsqlStreamAttribute`
- `Kafka.Ksql.Linq.Core.Abstractions.KsqlTableAttribute`
- `Kafka.Ksql.Linq.Core.Abstractions.TopicAttribute`
- `Kafka.Ksql.Linq.Core.Abstractions.AvroTimestampAttribute`
- `Kafka.Ksql.Linq.Core.Abstractions.DateTimeFormatAttribute`
- `Kafka.Ksql.Linq.Core.Abstractions.DecimalPrecisionAttribute`
- `Kafka.Ksql.Linq.Core.Abstractions.KafkaIgnoreAttribute`

## 依存箇所例
| 削除対象 | 参照ファイル |
|-----------|--------------|
| `KsqlStreamAttribute` | `src/Serialization/Abstractions/AvroEntityConfiguration.cs`, `src/Core/Abstractions/EntityModel.cs`, `src/Query/Pipeline/DDLQueryGenerator.cs` |
| `KsqlTableAttribute` | `src/Serialization/Abstractions/AvroEntityConfiguration.cs`, `src/Core/Abstractions/EntityModel.cs`, `src/Query/Pipeline/DDLQueryGenerator.cs` |
| `TopicAttribute` | `src/Serialization/Abstractions/AvroEntityConfiguration.cs`, `src/Core/Abstractions/EntityModel.cs`, `src/Query/Pipeline/DDLQueryGenerator.cs`, `src/Messaging/KafkaConsumerManager.cs` |
| `AvroTimestampAttribute` | `src/Core/Window/WindowAggregatedEntitySet.cs`, `src/Core/Window/WindowedEntitySet.cs` |
| `DateTimeFormatAttribute` | `src/Serialization/Abstractions/AvroSchemaBuilder.cs`, `src/Serialization/Abstractions/UnifiedSchemaGenerator.cs` |
| `DecimalPrecisionAttribute` | `src/Serialization/Abstractions/AvroSchemaBuilder.cs`, `src/Serialization/Abstractions/UnifiedSchemaGenerator.cs` |
| `KafkaIgnoreAttribute` | `src/Serialization/Abstractions/AvroEntityConfiguration.cs`, `src/Serialization/Abstractions/AvroSchemaBuilder.cs`, `src/Serialization/Abstractions/UnifiedSchemaGenerator.cs` |

テストでは `tests/TopicFluentApiTests.cs` などが `TopicAttribute` を参照しているため、削除時に合わせて更新を行います。
