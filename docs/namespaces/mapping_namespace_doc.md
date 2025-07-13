# Kafka.Ksql.Linq.Mapping Namespace 責務定義書

## 概要
POCO モデルと key/value ペアの相互変換を担当する軽量コンポーネント群。
`Query` namespace から提供される `QuerySchema` メタ情報を利用し、
エンティティから Kafka 送信に必要な key/value を生成、また受信データを
POCO へ復元する責務を持つ。

## 主要クラス
- **`PocoMapper`**: 静的 API として `ToKeyValue` と `FromKeyValue` を提供。
  - 型変換、null 管理、複合キー対応を内部で処理。
- **`MappingRegistry`**: `PropertyMeta[]` から key/value 用クラスを動的生成し `KeyValueTypeMapping` として登録・取得する。
- 生成される型の名前と名前空間は、POCO の完全修飾名を小文字化し `-key` / `-value` を付与した ksqlDB スキーマ名に合わせる。
- **`KeyValueTypeMapping`**: 生成された `KeyType`/`ValueType` と各 `PropertyMeta[]` を保持する単純モデル。

## 責任境界
- ✅ `QuerySchema` に基づくプロパティ抽出と型変換
- ✅ スキーマ進化に伴うプロパティ追加・削除の吸収
- ❌ Kafka 通信やシリアライズ処理（Messaging/Serialization が担当）
- ❌ EntityModel の構築（Core が担当）

## Query との連携例
```csharp
var result = QueryAnalyzer.AnalyzeQuery<User, User>(q => q.Where(u => u.Id == 1));
var schema = result.Schema!;
var user = new User { Id = 1, Name = "Alice" };
var (key, value) = PocoMapper.ToKeyValue(user, schema);
```
上記で得た `key` と `value` を `KsqlContext` の `AddAsync` へ渡すことで、
クエリ定義と実際のメッセージ送信を疎結合に保てる。
