# EntitySet から Messaging までの利用ストーリー

🗕 2025年7月22日（JST）
🧐 作成者: 広夢・楠木

本ドキュメントでは、新アーキテクチャに基づく基本的な利用フローを示します。
`EntitySet<T>` で定義したクエリから `Messaging` 層を通じて Kafka にメッセージを
送信するまでの流れをサンプルコードと共に記載します。設計意図とベストプラクティス
を理解することで、各レイヤーの役割分担を把握してください。

## 1. 事前準備

1. `MappingManager` へモデルを登録する
2. `KsqlContext` を DI コンテナで管理する
3. `IKafkaProducer<T>` を `Messaging` 層から取得する

## 2. サンプルコード

```csharp
public class Payment
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
}

class PaymentContext : KsqlContext
{
    protected override void OnModelCreating(IModelBuilder builder)
    {
        builder.Entity<Payment>()
            .WithTopic("payments")
            .HasKey(p => p.Id);
    }
}

var services = new ServiceCollection();
services.AddSingleton<IMappingManager, MappingManager>();
services.AddKsqlContext<PaymentContext>();
services.AddKafkaMessaging();

var provider = services.BuildServiceProvider();
var ctx = provider.GetRequiredService<PaymentContext>();

await foreach (var (key, value) in ctx.EntitySet<Payment>().Select(p => p))
{
    await ctx.Messaging.AddAsync(key, value);
}
```

## 3. ベストプラクティス

- `MappingManager` への登録はアプリ起動時に一括で行う
- `KsqlContext` はスコープライフサイクルを推奨し、使い回しを避ける
- 送信前に `QueryBuilder` が生成した KSQL 文をログで確認する
- `Messaging` の `AddAsync` は失敗時に DLQ へ送る設定を有効にする
- 例外発生時は `IKafkaProducer` を再生成せず、リトライポリシーを利用

## 4. 参考資料

- [key_value_flow.md](./key_value_flow.md) – 各レイヤーの責務概要
- [fluent_api_initial_design.md](../fluent_api_initial_design.md)

