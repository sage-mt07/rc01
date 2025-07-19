# Query から AddAsync までのサンプルフロー

🗕 2025年7月20日（JST）
🧐 作成者: くすのき

このドキュメントでは、`EntitySet<T>` の LINQ クエリを解析して得たスキーマを利用し、`AddAsync` で Kafka にメッセージを送るまでの一連の手順を紹介します。サービス登録さえ済ませれば、そのまま利用できる形でまとめました。

```csharp
var services = new ServiceCollection();
services.AddSampleModels();
services.AddSingleton<SampleContext>();
var provider = services.BuildServiceProvider();
var ctx = provider.GetRequiredService<SampleContext>();

// LINQ クエリ定義
// QueryAnalyzer で KSQL スキーマ生成
var result = QueryAnalyzer.AnalyzeQuery<Order, Order>(
    src => src.Where(o => o.Amount > 100));
var schema = result.Schema!;

// key/value 抽出と送信
var order = new Order { OrderId = 1, UserId = 10, ProductId = 5, Quantity = 2 };
var (key, value) = PocoMapper.ToKeyValue(order, schema);
await ctx.Set<Order>().AddAsync(order);
```

`ExtractKeyParts` で取得した複合キーは Type 情報を保持するため、安全に `BuildTypedKey` で変換できます。

このサンプルを参考に、クエリ定義からメッセージ送信までを DI コンテナ上のサービスで完結させてみましょう。以下のポイントも意識すると、より安全に運用できます。

- `QueryAnalyzer` の結果はキャッシュし、何度も解析し直さない
- `AddAsync` は失敗時にリトライするか、DLQ へ送る仕組みを用意する
- `KsqlContext` はスコープライフサイクルで生成し、使い回しを避ける

