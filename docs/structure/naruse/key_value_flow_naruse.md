# Key-Value Flow (Naruse View)

この文書は [shared/key_value_flow.md](../shared/key_value_flow.md) を参照し、実装担当の鳴瀬がクラス連携の観点から再整理したものです。

## 依存順

```
Query -> POCO-Query Mapping -> KsqlContext -> Messaging -> Serialization -> Kafka
```

## 責務分離

| コンポーネント | 主なクラス例 | 役割 |
|---------------|-------------|------|
| **Builder** | `KsqlContextBuilder` | DSL設定を集約し `KsqlContext` を生成 |
| **Pipeline** | `QueryBuilder` | LINQ式を解析して key/value を抽出 |
| **Mapping** | `PocoMapper` | POCO と KSQL・Key/Value の相互変換 |
| **Context** | `KsqlContext` | Produce/Consume の統括と DI 初期化 |
| **Serializer** | `AvroSerializer` | key/value を Avro フォーマットへ変換 |
| **Messaging** | `KafkaProducer`, `KafkaConsumer` | トピック単位の送受信を担当 |

## LINQ式ベースの流れ

1. アプリケーションは `EntitySet<T>` で LINQ クエリを記述します。
2. `QueryBuilder` が式ツリーを解析し、`QuerySchema` を生成します。
3. `PocoMapper` の `ToKeyValue` により key/value が生成されます。
4. `KsqlContextBuilder` が各種オプションをまとめ `KsqlContext` を構築します。
5. `KsqlContext` から `KafkaProducer` または `KafkaConsumer` を取得し、メッセージの送受信を実行します。
6. `AvroSerializer` がオブジェクトをシリアライズし、Kafka ブローカーへ配信します。

## PocoMapper API

```csharp
namespace Kafka.Ksql.Linq.Mapping;

public static class PocoMapper
{
    public static (object Key, TEntity Value) ToKeyValue<TEntity>(TEntity entity, QuerySchema schema) where TEntity : class;
    public static TEntity FromKeyValue<TEntity>(object? key, TEntity value, QuerySchema schema) where TEntity : class;
}
```

### Query → PocoMapper → KsqlContext サンプル

```csharp
var modelBuilder = new ModelBuilder();
modelBuilder.Entity<User>().WithTopic("users").HasKey(u => u.Id);
var model = modelBuilder.GetEntityModel<User>()!;


var schema = QueryAnalyzer.AnalyzeQuery<User, User>(q => q.Where(u => u.Id == 1)).Schema!;

var query = context.Set<User>().Where(u => u.Id == 1);
var entity = new User { Id = 1, Name = "Alice" };
var (key, value) = PocoMapper.ToKeyValue(entity, schema);
await context.AddAsync(entity);
```

上記のように `Query` から生成したエンティティを `PocoMapper` で key/value に変換し、`KsqlContext` 経由で Kafka へ送信します。
