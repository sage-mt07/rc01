# Window処理による時系列集約

## ⚠️ 重要な設計思想

**各時間足ごとに個別のPOCOクラスやトピックを作成する必要はありません。**

Kafka.Ksql.Linqでは、1つのエンティティで複数の時間足を同時に処理できる統一的なWindow機能を提供しています。

## 🏗️ 基本的な使い方

### ❌ 誤った方法（非推奨）
```csharp
// これは不要で非効率です
[Topic("rate_1min")] public class Rate1MinCandle { ... }
[Topic("rate_5min")] public class Rate5MinCandle { ... }
[Topic("rate_10min")] public class Rate10MinCandle { ... }
```

### ✅ 正しい方法（推奨）
```csharp
[Topic("rates")]
[KsqlStream]
public class Rate
{
    public string Symbol { get; set; }
    public decimal Price { get; set; }
    [AvroTimestamp] public DateTimeOffset Timestamp { get; set; }
}

// 1つのPOCOで全時間足に対応
[Topic("rate_candles")]
[KsqlTable]
public class RateCandle
{
    public string Symbol { get; set; }
    [AvroTimestamp] public DateTime WindowStart { get; set; }
    [AvroTimestamp] public DateTime WindowEnd { get; set; }
    public int WindowMinutes { get; set; }  // 時間足識別子
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public int Count { get; set; }
}

public class MyKsqlContext : KsqlContext
{
    protected override void OnModelCreating(IModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Rate>();
        
        // 複数時間足を一度に定義
        modelBuilder.Entity<RateCandle>()
            .HasQueryFrom<Rate>(q => 
                q.Window(new[] { 1, 5, 15, 60 })  // 1分, 5分, 15分, 60分足
                 .GroupBy(r => r.Symbol)
                 .Select(g => new RateCandle
                 {
                     Symbol = g.Key,
                     WindowStart = g.Window.Start,
                     WindowEnd = g.Window.End,
                     WindowMinutes = g.Window.Size.Minutes,
                     Open = g.OrderBy(r => r.Timestamp).First().Price,
                     High = g.Max(r => r.Price),
                     Low = g.Min(r => r.Price),
                     Close = g.OrderByDescending(r => r.Timestamp).First().Price,
                     Count = g.Count()
                 }));
    }
}
```

## 🔄 データ処理パターン

### 全時間足統合処理
```csharp
// 全ての時間足データを統一的に処理
await context.Set<RateCandle>()
    .ForEachAsync(candle => 
    {
        Console.WriteLine($"{candle.WindowMinutes}分足: {candle.Symbol} " +
                         $"OHLC({candle.Open}, {candle.High}, {candle.Low}, {candle.Close})");
    });
```

### 特定時間足フィルタリング
```csharp
// 実行時に特定の時間足のみ処理
await context.Set<RateCandle>()
    .Window(5)  // 5分足のみ
    .ForEachAsync(candle =>
    {
        Console.WriteLine($"5分足のみ: {candle.Symbol} Close: {candle.Close}");
    });
```

### 複数時間足の並列処理
```csharp
// 異なる時間足を並列で処理
var tasks = new[]
{
    ProcessTimeFrame(1),   // 1分足処理
    ProcessTimeFrame(5),   // 5分足処理
    ProcessTimeFrame(60)   // 60分足処理
};

await Task.WhenAll(tasks);

async Task ProcessTimeFrame(int minutes)
{
    await context.Set<RateCandle>()
        .Window(minutes)
        .ForEachAsync(candle =>
        {
            // 時間足別の専用処理
            await ProcessCandle(candle, minutes);
        });
}
```

## 📊 Final Topic（確定データ）の活用

Window処理では、各時間足ごとに確定データ用のトピックが自動生成されます：

- `rates_window_1_final` （1分足確定データ）
- `rates_window_5_final` （5分足確定データ）
- `rates_window_60_final` （60分足確定データ）

```csharp
// 確定データの処理
var finalConsumer = new WindowFinalConsumer(rocksDbStore, loggerFactory);

// 各時間足の確定データを個別処理
await finalConsumer.SubscribeToFinalizedWindows("rates", 1, async (finalMessage) =>
{
    Console.WriteLine($"1分足確定: {finalMessage.WindowKey} - {finalMessage.EventCount}件");
});

await finalConsumer.SubscribeToFinalizedWindows("rates", 5, async (finalMessage) =>
{
    Console.WriteLine($"5分足確定: {finalMessage.WindowKey} - {finalMessage.EventCount}件");
});

// 履歴データの取得
var historical = finalConsumer.GetFinalizedWindowsBySize(5, DateTime.UtcNow.AddHours(-1));
Console.WriteLine($"過去1時間の5分足データ: {historical.Count}件");
```

## ⚡ 動的時間足追加

実行時に新しい時間足を追加することも可能です：

```csharp
// 実行時に30分足を追加
var dynamicConfig = new WindowConfiguration<Rate>
{
    TopicName = "rates",
    Windows = new[] { 30 },  // 30分足を新規追加
    GracePeriod = TimeSpan.FromSeconds(3),
    FinalTopicProducer = producer,
    AggregationFunc = rates => CreateOHLCAggregation(rates)
};

windowManager.RegisterWindowProcessor(dynamicConfig);
```

## 🎯 設定例

### appsettings.json
```json
{
  "KsqlDsl": {
    "Entities": [
      {
        "Entity": "Rate",
        "SourceTopic": "rates",
        "Windows": [1, 5, 15, 60],  // 複数時間足を配列で指定
        "EnableCache": true
      }
    ]
  }
}
```

### WindowConfiguration
```csharp
var config = new WindowConfiguration<Rate>
{
    TopicName = "rates",
    Windows = new[] { 1, 5, 15, 60 },  // 全時間足を一括設定
    GracePeriod = TimeSpan.FromSeconds(3),
    RetentionHours = 24,
    AggregationFunc = rates => CreateOHLCAggregation(rates)
};
```

## 💡 ベストプラクティス

### ✅ 推奨
- 1つのエンティティで複数時間足を処理
- `WindowMinutes`プロパティで時間足を識別
- 実行時フィルタリングで特定時間足を処理
- Final Topicで確定データを安全に取得

### ❌ 非推奨
- 時間足ごとに個別POCOクラス作成
- 時間足ごとに個別トピック手動作成
- Window機能を使わずに手動集約
- 未確定データでの重要な判断

## 🔍 トラブルシューティング

### Q: 各時間足で異なる処理をしたい場合は？
A: `Set<T>().Window(分数)` を利用して、時間足別メソッドで処理してください。

### Q: リアルタイム処理と履歴データ処理を分けたい場合は？
A: リアルタイムは通常の`ForEachAsync()`、履歴は`WindowFinalConsumer.GetFinalizedWindowsBySize()`を使用してください。

### Q: 時間足を後から追加/削除したい場合は？
A: `WindowFinalizationManager.RegisterWindowProcessor()`で動的追加可能です。削除は新しい設定での再起動が推奨されます。

---

## 📖 関連ドキュメント

- [Window Finalization詳細](./docs/window-finalization.md)
- [Error Handling with Windows](./docs/error-handling-windows.md)
- [Performance Optimization](./docs/performance-optimization.md)