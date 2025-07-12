# Kafka.Ksql.Linq.Serialization Namespace 責務定義書

## 概要
Apache Kafka での Avro シリアライゼーション機能を提供するnamespace群。Schema Registry との連携によるスキーマ管理とエンティティのシリアライゼーション/デシリアライゼーションを担当。

---

## Namespace別責務

### 1. `Kafka.Ksql.Linq.Serialization.Abstractions`
**責務**: シリアライゼーション機能の抽象化層・設定管理

#### 核心コンポーネント
- **`AvroEntityConfiguration`**: エンティティごとのシリアライゼーション設定（トピック名、キー設定、検証設定）
- **`AvroEntityConfigurationBuilder<T>`**: Fluent API によるエンティティ設定構築
- **`IAvroSerializationManager<T>`**: エンティティ固有のシリアライゼーション管理インターフェース
- **`SerializerPair<T>` / `DeserializerPair<T>`**: キー・バリューペアのシリアライザ・デシリアライザ保持

#### 責務境界
- ✅ エンティティ設定の定義・構築・検証
- ✅ シリアライゼーション統計の提供
- ❌ 具体的なシリアライゼーション処理（下位層に委譲）

---

### 2. `Kafka.Ksql.Linq.Serialization.Avro.Core`
**責務**: Avro シリアライゼーションの具体実装とスキーマ処理

#### 核心コンポーネント
- **`AvroSerializerFactory`**: シリアライザ・デシリアライザの生成ファクトリ
- **`UnifiedSchemaGenerator`**: 型からAvroスキーマへの統一変換処理
- **`AvroSchemaInfo`**: スキーマ登録情報の保持（ID、スキーマ文字列、メタデータ）

#### 責務境界
- ✅ Confluent ライブラリとの連携
- ✅ C# 型 → Avro スキーマ変換
- ✅ プリミティブ型・複合型キーの処理
- ❌ スキーマのバージョン管理（Management層に委譲）

---

### 3. `Kafka.Ksql.Linq.Serialization.Avro.Cache`
**責務**: シリアライザのキャッシュ管理とパフォーマンス最適化

#### 核心コンポーネント
- **`AvroSerializerCache`**: エンティティ型ベースのシリアライザキャッシュ
- **`AvroEntitySerializationManager<T>`**: エンティティ固有のキャッシュ済みシリアライゼーション管理

#### 責務境界
- ✅ シリアライザ・デシリアライザのメモリキャッシュ
- ✅ キャッシュヒット/ミス統計の管理
- ✅ ラウンドトリップ検証
- ❌ 永続化キャッシュ（メモリ内のみ）

---

### 4. `Kafka.Ksql.Linq.Serialization.Avro.Management`
**責務**: スキーマのライフサイクル管理とSchema Registry連携

#### 核心コンポーネント
- **`AvroSchemaRegistrationService`**: Schema Registry への一括スキーマ登録
- **`AvroSchemaVersionManager`**: スキーマバージョン管理・アップグレード処理
- **`AvroSchemaBuilder`**: スキーマ生成とバリデーション
- **`AvroSchemaRepository`**: 登録済みスキーマ情報の管理

#### 責務境界
- ✅ Schema Registry との全通信
- ✅ スキーマバージョニング・互換性チェック
- ✅ スキーマ進化の管理
- ❌ 個別エンティティのシリアライゼーション（Core層に委譲）

---

### 5. `Kafka.Ksql.Linq.Serialization.Avro.Exceptions`
**責務**: シリアライゼーション関連例外の定義

#### 核心コンポーネント
- **`SchemaRegistrationFatalException`**: Fail-Fast設計用の致命的例外
- **`AvroSchemaRegistrationException`**: 一般的なスキーマ登録例外

#### 責務境界
- ✅ 運用者向けエラー情報の提供
- ✅ Fail-Fast 設計のサポート
- ❌ エラー処理ロジック（呼び出し元の責務）

---

### 6. `Kafka.Ksql.Linq.Serialization.Avro` (Root)
**責務**: レジリエンス機能とリトライ処理

#### 核心コンポーネント
- **`ResilientAvroSerializerManager`**: Schema Registry 操作の堅牢化（リトライ・フォールバック）

#### 責務境界
- ✅ ネットワーク障害等に対するリトライ処理
- ✅ 詳細な失敗ログとFail-Fast実行
- ❌ ビジネスロジックレベルの例外処理

---

## 依存関係フロー

```
Abstractions (設定・インターフェース)
    ↓
Cache (パフォーマンス最適化)
    ↓
Core (具体実装)
    ↓
Management (Schema Registry連携)
    ↓
Root (レジリエンス)
```

## 設計原則

1. **単一責任**: 各namespaceは明確に分離された責務を持つ
2. **Fail-Fast**: 致命的エラーは即座にアプリケーション停止
3. **キャッシュファースト**: パフォーマンス最適化のための積極的キャッシュ
4. **スキーマ進化対応**: バージョン管理とスキーマ互換性の保証