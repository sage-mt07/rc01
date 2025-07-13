# Query to AddAsync Flow Sample

🗕 2025年7月27日（JST）
🧐 作成者: naruse

`EntitySet<T>` の LINQ クエリを `QueryAnalyzer` で解析し、`MappingManager` が生成した key/value を `KsqlContext` の `AddAsync` へ渡すまでのサンプルです。DI に登録したサービスのみで完結します。

```csharp
var services = new ServiceCollection();
services.AddSampleModels();              // MappingManager とモデル登録
services.AddSingleton<IMappingManager, MappingManager>();
services.AddSingleton<SampleContext>();
var provider = services.BuildServiceProvider();
var ctx = provider.GetRequiredService<SampleContext>();
var mapping = provider.GetRequiredService<IMappingManager>();

// LINQ クエリ定義
// QueryAnalyzer で KSQL スキーマ生成
var result = QueryAnalyzer.AnalyzeQuery<Order, Order>(
    src => src.Where(o => o.Amount > 100));
var schema = result.Schema!;

// key/value 抽出と送信
var order = new Order { OrderId = 1, UserId = 10, ProductId = 5, Quantity = 2 };
var parts = mapping.ExtractKeyParts(order);
var key = KeyExtractor.BuildTypedKey(parts);
await ctx.Set<Order>().AddAsync(order);
```

`ExtractKeyParts` で取得した複合キーは Type 情報を保持するため、安全に `BuildTypedKey` で変換できます。

この流れにより、クエリ定義からメッセージ送信までを DI コンテナ上のサービスで完結させることができます。
