# Key-Value Flow Architecture (POCO ↔ Kafka)

## 1. 概要

本資料は、Query namespace に定義された POCO および LINQ式から Kafka へ送信するフロー（Produce）と、Kafka から受信して POCO に復元するフロー（Consume）を一貫して設計するための責務分解図である。

---

## 2. 全体構造図（双方向）

[Query] ⇄ [KsqlContext] ⇄ [Messaging] ⇄ [Serialization] ⇄ [Kafka]


## 3. Produce Flow（POCO → Kafka）

[Query/EntitySet<T>]
↓ LINQ式, POCO
[KsqlContext/ExtractKeyValue()]
↓ T → key, value
[Messaging/IKafkaProducer<T>.Produce()]
↓ key, value
[Serialization/AvroSerializer]
↓ byte[]
[Kafka]
→ Topic送信

yaml
コピーする
編集する

### 🧱 責務一覧

| レイヤー     | クラス名             | 主な責務                                  |
|--------------|----------------------|-------------------------------------------|
| Query        | EntitySet<T>         | LINQ式とPOCOを提供                         |
| KsqlContext  | ExtractKeyValue()    | LINQ式に基づく key-value抽出              |
| Messaging    | IKafkaProducer<T>    | メッセージ送信、トピック指定              |
| Serialization| AvroSerializer       | key/value の Avro変換（Confluent）        |
| Kafka        | Kafka Broker         | メッセージ配信                            |

---

## 4. Consume Flow（Kafka → POCO）

[Kafka]
↓ メッセージ受信
[Serialization/AvroDeserializer]
↓ key, value（byte[] → object）
[Messaging/IKafkaConsumer<TKey, TValue>]
↓ POCO再構成（TKey, TValue）
[Application/Callback or Pipeline]
→ アプリケーションロジックへ渡す



### 🧱 責務一覧

| レイヤー     | クラス名               | 主な責務                                     |
|--------------|------------------------|----------------------------------------------|
| Kafka        | Kafka Broker           | メッセージ受信                                |
| Serialization| AvroDeserializer       | Avro → POCO 変換（Confluent）                |
| Messaging    | IKafkaConsumer<TKey, TValue> | メッセージ処理, POCO復元                 |
| Application  | Consumer Handler       | アプリロジックへの通知・後処理              |

---

## 5. 注意点

- 全体のKey定義はLINQ式で統一（POCOの属性依存を排除）。
- key/valueのAvro変換はConfluent公式に完全依存。
- `IKafkaConsumer` は再生成されたTKey/TValueの型安全性を保持。
- 各構成はDIにより初期化、KsqlContextが統括。

## 6. 利用シナリオ: EntitySet から Messaging まで

LINQ クエリをどのように `Kafka` 配信までつなぐかを示すため、代表的なシーケンスとコード例を以下にまとめる。

### シーケンス図

```mermaid
sequenceDiagram
    participant App as Application
    participant Query as EntitySet<T>
    participant Builder as QueryBuilder
    participant Mapping as MappingManager
    participant Context as KsqlContext
    participant Msg as KafkaProducer
    App->>Query: LINQクエリ作成
    Query->>Builder: 式ツリー解析
    Builder->>Mapping: モデル問い合わせ
    Mapping->>Context: key/value生成
    Context->>Msg: Produce(key, value)
```

### サンプルコード

```csharp
var ctx = new MyKsqlContext(options);
var set = ctx.Set<User>();

var query = set.Where(u => u.Id == 1);
var builder = new QueryBuilder(ctx.Model);
var mapping = ctx.MappingManager;

var ksql = builder.Build(query);
var entity = new User { Id = 1, Name = "Alice" };
var (key, value) = mapping.ExtractKeyValue(entity);
await ctx.AddAsync(entity);
```

### ベストプラクティス

- `MappingManager` へ登録するモデルは `OnModelCreating` で一括定義する。
- `QueryBuilder` から返される KSQL 文はデバッグログで確認しておく。
- `KsqlContext` のライフサイクルは DI コンテナに任せ、使い回しを避ける。

### アンチパターン

- `MappingManager` を毎回 `new` して登録し直す。 → モデル漏れや性能低下につながる。
- LINQ クエリ側で複雑なロジックを組み込み、`QueryBuilder` の解析失敗を誘発する。

### 異常系の流れ

1. `MappingManager` に登録されていないエンティティを渡した場合、`KeyNotFoundException` が発生する。
2. `KsqlContext` との接続に失敗した場合は `KafkaException` を上位へ伝搬する。

