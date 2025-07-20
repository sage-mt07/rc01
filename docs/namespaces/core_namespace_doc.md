# Kafka.Ksql.Linq.Core Namespace 責務ドキュメント

## 概要
Kafka.Ksql.Linq.Coreは、Apache KafkaとKsqlDBを使ったストリーミング処理のためのLINQライクなライブラリのコア層です。
旧バージョンで使用していた各種Attributeは廃止され、モデル設定は `ModelBuilder` を中心とした Fluent API に統一されました。

## Namespace構成と責務

### `Kafka.Ksql.Linq.Core.Abstractions`
**責務**: コア抽象定義・インターフェース層
- **主要インターフェース**:
  - `IKsqlContext`: DbContext風の統一インターフェース
  - `IEntitySet<T>`: クエリ・更新共通操作の統一インターフェース
  - `ISerializationManager<T>`: シリアライザ共通インターフェース
 - **主要クラス**:
   - `ModelBuilder`: Fluent API設定エントリポイント
   - `EntityModelBuilder<T>`: エンティティ単位のモデル構築
   - `KeyExtractor`: キー抽出ヘルパー
 - **エラーハンドリング**: `ErrorAction`, `ErrorHandlingPolicy`, `CircuitBreakerHandler`
- **ウィンドウ処理**: `IWindowedEntitySet<T>`, `WindowAggregationConfig`


### `Kafka.Ksql.Linq.Core.Configuration`
**責務**: Core層設定管理
- `CoreSettings`: Kafka接続・検証モード設定
- `CoreSettingsProvider`: 設定変更の監視・通知
- `SchemaRegistrySection`: Schema Registry 接続設定

### `Kafka.Ksql.Linq.Core.Context`
**責務**: コンテキスト基底実装
- `KafkaContextCore`: モデル構築・エンティティセット管理の基底クラス
- `KafkaContextOptions`: コンテキスト設定

### `Kafka.Ksql.Linq.Core.Extensions`
**責務**: 拡張メソッド群
- `CoreExtensions`: EntityModel・Type・PropertyInfo拡張
- `EntityModelWindowExtensions`: ウィンドウ機能拡張
- `LoggerFactoryExtensions`: ログ機能拡張

### `Kafka.Ksql.Linq.Core.Modeling`
**責務**: エンティティモデル構築
- `ModelBuilder`: Fluent APIによるモデル設定
- `EntityModelBuilder<T>`: 個別エンティティ設定
- `WindowModelBuilderExtensions`: ウィンドウ集約設定

### `Kafka.Ksql.Linq.Core.Models`
**責務**: コアモデル・ヘルパー
- `KeyExtractor`: エンティティキー抽出・変換
- `ProducerKey`, `ConsumerKey`: Kafka操作識別子

### `Kafka.Ksql.Linq.Core.Window`
**責務**: ウィンドウ処理実装
- `WindowedEntitySet<T>`: ウィンドウ化エンティティセット
- `WindowAggregatedEntitySet<T>`: ウィンドウ集約結果
- `WindowCollection<T>`: 複数ウィンドウ管理

### `Kafka.Ksql.Linq.Core.Exceptions`
**責務**: Core層例外定義
- `CoreException`: 基底例外
- `EntityModelException`: エンティティモデル例外
- `CoreValidationException`: 検証例外

## 設計原則

### 依存関係制約
- **上位層への依存禁止**: Communication, Messaging, Serialization, Monitoring層への依存は禁止
- **純粋性重視**: 副作用のない関数型設計を採用
- **ログフリー**: Infrastructure層でログ処理を実装

-### 主要パターン
- **Fluent API主体**: `EntityModelBuilder<T>` で `HasTopic()` や `HasKey()` を記述
- **Fluent API**: ModelBuilderによる設定の補完
- **LINQ互換**: `IAsyncEnumerable<T>`ベースの統一インターフェース
- **エラーハンドリング**: Skip/Retry/DLQの3段階対応

## 主要概念

-### エンティティモデル
- **Stream vs Table**: キープロパティの有無で自動判定
- **複合キー**: プロパティ定義順でキーを構成
- **ウィンドウ処理**: `[AvroTimestamp]`属性によるイベントタイム指定

### ウィンドウ処理
- **Tumbling Window**: 固定サイズの非重複ウィンドウ
- **集約関数**: Sum, Count, Max, Min, Latest/EarliestByOffset
- **遅延許容**: GracePeriod設定による遅延データ処理

### エラーハンドリング
- **回路ブレーカー**: 連続失敗時の自動停止・回復
- **指数バックオフ**: リトライ間隔の動的調整
- **条件付きリトライ**: 例外タイプによる選択的リトライ