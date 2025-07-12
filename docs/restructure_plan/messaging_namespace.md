# Messaging Namespace 再設計方針

## 背景
`architecture_diff_20250711.md` で指摘された通り、KafkaProducer/Consumer のプール構造が残存し、Confluent 公式実装との整合が不完全な状態です。さらに `architecture_restart_20250711.md` に沿い、公式 AvroSerializer/Deserializer 採用を前提とした構造再構築を行います。`ProducerPoolException` など旧構造の残骸を整理し、責務の明確化とファイル統合を実施します。

## 主要変更点
1. **Producer/Consumer のプール構造廃止**: `PooledProducer`, `PooledConsumer` など旧Pool関連クラスを削除し、単一インスタンス利用に統一。
2. **Confluent依存の明示化**: `KafkaProducer` / `KafkaConsumer` は Confluent.Kafka のラッパーとして実装し、接続設定は `Application` レイヤーへ委譲。
3. **Messaging内責務の最小化**: Dslと連携するための `IKafkaProducer<T>`, `IKafkaConsumer<TKey, TValue>` インターフェースのみを残し、設定管理は `Configuration` へ統合。
4. **例外整理**: `ProducerPoolException` など不要な例外を削除し、`KafkaProducerManagerException` 等に一本化。

## 対応ファイル
- `src/Messaging/Producers/Core/PooledProducer.cs` （削除）
- `src/Messaging/Consumers/Core/PooledConsumer.cs` （削除）
- `src/Messaging/Consumers/Core/ConsumerInstance.cs` （再設計検討）
- `src/Messaging/Producers/KafkaProducerManager.cs` （接続管理の単純化）
- `src/Messaging/Consumers/KafkaConsumerManager.cs` （同上）
- `src/Messaging/Exceptions/ProducerPoolException.cs` （削除）

## 備考
プール廃止に伴い、`KsqlContext` 内で保持していた接続キャッシュ処理も再検討します。Confluent 公式の `IProducer` / `IConsumer` を直接管理する方針に合わせ、テストコードも修正予定です。
