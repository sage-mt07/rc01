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
| **Mapping** | `MappingManager` | POCO と KSQL・Key/Value の対応を管理 |
| **Context** | `KsqlContext` | Produce/Consume の統括と DI 初期化 |
| **Serializer** | `AvroSerializer` | key/value を Avro フォーマットへ変換 |
| **Messaging** | `KafkaProducer`, `KafkaConsumer` | トピック単位の送受信を担当 |

## LINQ式ベースの流れ

1. アプリケーションは `EntitySet<T>` で LINQ クエリを記述します。
2. `QueryBuilder` が式ツリーを解析し、`MappingManager` へマッピング情報を問い合わせます。
3. `MappingManager` の `ExtractKeyValue` により key/value が生成されます。
4. `KsqlContextBuilder` が各種オプションをまとめ `KsqlContext` を構築します。
5. `KsqlContext` から `KafkaProducer` または `KafkaConsumer` を取得し、メッセージの送受信を実行します。
6. `AvroSerializer` がオブジェクトをシリアライズし、Kafka ブローカーへ配信します。

## MappingManager 初期API案

```csharp
namespace Kafka.Ksql.Linq.Mapping;

public interface IMappingManager
{
    void Register<TEntity>(EntityModel model) where TEntity : class;
    (object Key, TEntity Value) ExtractKeyValue<TEntity>(TEntity entity) where TEntity : class;
}

public class MappingManager : IMappingManager
{
    // EntityModel を型ごとに保持
    private readonly Dictionary<Type, EntityModel> _models = new();

    public void Register<TEntity>(EntityModel model) where TEntity : class
    {
        _models[typeof(TEntity)] = model;
    }

    public (object Key, TEntity Value) ExtractKeyValue<TEntity>(TEntity entity) where TEntity : class
    {
        var model = _models[typeof(TEntity)];
        var key = KeyExtractor.ExtractKeyValue(entity, model);
        return (key, entity);
    }
}
```

### Query → MappingManager → KsqlContext サンプル

```csharp
var modelBuilder = new ModelBuilder();
modelBuilder.Entity<User>().WithTopic("users").HasKey(u => u.Id);
var model = modelBuilder.GetEntityModel<User>()!;

var mapping = new MappingManager();
mapping.Register<User>(model);

var query = context.Set<User>().Where(u => u.Id == 1);
var entity = new User { Id = 1, Name = "Alice" };
var (key, value) = mapping.ExtractKeyValue(entity);
await context.AddAsync(entity);
```

上記のように `Query` から生成したエンティティを `MappingManager` で key/value に変換し、`KsqlContext` 経由で Kafka へ送信します。
