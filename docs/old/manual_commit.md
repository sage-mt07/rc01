# Manual Commitの利用例

Kafka.Ksql.Linq では、自動コミットを無効化し、メッセージ単位で `CommitAsync()` や `NegativeAckAsync()` を呼び出せるモードを提供しています。ここでは、`WithManualCommit()` を使用した場合の `ForEachAsync()` の振る舞いを説明します。

```csharp
await foreach (var msg in context.HighValueOrders.WithManualCommit().ForEachAsync())
{
    try
    {
        Process(msg.Value);
        await msg.CommitAsync();
    }
    catch
    {
        await msg.NegativeAckAsync();
    }
}
```

`WithManualCommit()` を指定しない場合、`ForEachAsync()` はエンティティ `T` をそのまま返します。自動コミットが行われるため、手動で `CommitAsync()` などを呼び出す必要はありません。

```csharp
await foreach (var order in context.Orders.ForEachAsync())
{
    Console.WriteLine(order.OrderId);
}
```
