# Kafka.Ksql.Linq.Messaging 責務ドキュメント

## 概要
Kafka メッセージング機能の型安全な抽象化層を提供する namespace。Producer/Consumer の統一管理、設定管理、エラーハンドリング（DLQ）を担当。新アーキテクチャでは **key/value の送受信のみを担う最小 API** へ責務を絞る。

## 主要な責務

### 1. Abstractions - インターフェース定義
- **`IKafkaProducer<T>`**: 型安全な Producer インターフェース
- **`IKafkaConsumer<TValue, TKey>`**: 型安全な Consumer インターフェース

**設計意図**: 型安全性確保、テスタビリティ向上、既存 Avro 実装との統合

### 2. Configuration - 設定管理
- **`CommonSection`**: Kafka ブローカー共通設定（接続、セキュリティ）
- **`ProducerSection`**: Producer 固有設定（確認応答、圧縮、冪等性）
- **`ConsumerSection`**: Consumer 固有設定（グループ、オフセット、フェッチ）
- **`SchemaRegistrySection`**: Schema Registry 接続設定
- **`TopicSection`**: トピック別設定（Producer/Consumer 両方を含む）

**設計意図**: 設定の階層化、運用時の柔軟性確保

### 3. Producers - メッセージ送信
#### Core クラス
- **`KafkaProducer<T>`**: 統合型安全 Producer（Pool 削除、Confluent.Kafka 完全委譲）
- **`KafkaProducerManager`**: Producer の型安全管理（事前確定・キャッシュ）

#### 送信結果
- **`KafkaDeliveryResult`**: 単一メッセージ送信結果

#### DLQ（Dead Letter Queue）
- **`DlqProducer`**: デシリアライズ失敗データの DLQ 送信
- **`DlqEnvelope`**: DLQ メッセージ形式

**設計意図**: EF風API、型安全性確保、エラー耐性

### 4. Consumers - メッセージ消費
#### Core クラス
- **`KafkaConsumer<TValue, TKey>`**: 統合型安全 Consumer
- **`KafkaConsumerManager`**: Consumer の型安全管理
- **`KafkaBatch<TValue, TKey>`**: バッチ消費結果

#### プール管理（廃止予定）
- **`PooledConsumer`**: プールされた Consumer（Pool 削除方針）
- **`ConsumerInstance`**: Consumer インスタンス管理

**設計意図**: Pool 削除によるシンプル化、購読パターンの統一

### 5. Contracts - エラーハンドリング契約
- **`IErrorSink`**: エラーレコード処理インターフェース（DLQ送信等）

### 6. Models - データ構造
- **`DlqEnvelope`**: DLQ メッセージのエンベロープ形式
  - 元メッセージ情報（Topic、Partition、Offset）
  - エラー情報（例外タイプ、メッセージ、スタックトレース）
  - デバッグ用ヘッダー復元

### 7. Internal - 内部実装
- **`ErrorHandlingContext`**: エラーハンドリング実行コンテキスト
  - リトライ制御
  - カスタムハンドラー実行
  - DLQ 送信判定

### 8. Exceptions - 例外定義
- **`KafkaConsumerManagerException`**: Consumer 管理例外
- **`KafkaProducerManagerException`**: Producer 管理例外  
- **`KafkaTopicConflictException`**: トピック設定競合例外

## アーキテクチャ特徴

### 型安全性の確保
- 全ての Producer/Consumer が型パラメータ `<T>` を持つ
- EntityModel を通じたメタデータ管理
- コンパイル時の型チェック

### Pool 削除による簡素化
- 従来の Pool 管理を廃止
- Confluent.Kafka への完全委譲
- リソース管理の簡素化

### 統一されたエラーハンドリング
- DLQ による失敗メッセージの保存
- デシリアライゼーション失敗の自動検出
- カスタムエラーハンドラーのサポート

### EF Core 風 API
- Manager クラスによる事前確定管理
- キャッシュによる性能向上
- 設定の階層化

## 主要な設計判断

1. **Pool 削除**: 複雑性削減のため Producer/Consumer プールを廃止
2. **型安全性優先**: 実行時エラーを防ぐため型パラメータを全面採用
3. **Confluent.Kafka 委譲**: 低レベル実装を Confluent.Kafka に完全委譲
4. **DLQ 標準装備**: 運用時のデータロスト防止のため DLQ を標準実装