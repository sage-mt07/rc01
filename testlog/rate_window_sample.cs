using Kafka.Ksql.Linq.Application;
using Kafka.Ksql.Linq.Core.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

// 元のレートデータPOCO
[Topic("rates")]
[KsqlStream]
public class Rate
{
    public string Symbol { get; set; }
    
    public decimal Bid { get; set; }
    
    public decimal Ask { get; set; }
    
    [AvroTimestamp]
    public DateTimeOffset Timestamp { get; set; }
}

// 1分足集約結果POCO
[Topic("rate_candles_1min")]
[KsqlTable]
public class Rate1MinCandle
{
    public string Symbol { get; set; }
    
    [AvroTimestamp]
    public DateTime WindowStart { get; set; }
    
    [AvroTimestamp]
    public DateTime WindowEnd { get; set; }
    
    public decimal OpenBid { get; set; }
    public decimal HighBid { get; set; }
    public decimal LowBid { get; set; }
    public decimal CloseBid { get; set; }
    
    public decimal OpenAsk { get; set; }
    public decimal HighAsk { get; set; }
    public decimal LowAsk { get; set; }
    public decimal CloseAsk { get; set; }
    
    public int TickCount { get; set; }
}

// 5分足集約結果POCO
[Topic("rate_candles_5min")]
[KsqlTable]
public class Rate5MinCandle
{
    public string Symbol { get; set; }
    
    [AvroTimestamp]
    public DateTime WindowStart { get; set; }
    
    [AvroTimestamp]
    public DateTime WindowEnd { get; set; }
    
    public decimal OpenBid { get; set; }
    public decimal HighBid { get; set; }
    public decimal LowBid { get; set; }
    public decimal CloseBid { get; set; }
    
    public decimal OpenAsk { get; set; }
    public decimal HighAsk { get; set; }
    public decimal LowAsk { get; set; }
    public decimal CloseAsk { get; set; }
    
    public int TickCount { get; set; }
}

// 10分足集約結果POCO
[Topic("rate_candles_10min")]
[KsqlTable]
public class Rate10MinCandle
{
    public string Symbol { get; set; }
    
    [AvroTimestamp]
    public DateTime WindowStart { get; set; }
    
    [AvroTimestamp]
    public DateTime WindowEnd { get; set; }
    
    public decimal OpenBid { get; set; }
    public decimal HighBid { get; set; }
    public decimal LowBid { get; set; }
    public decimal CloseBid { get; set; }
    
    public decimal OpenAsk { get; set; }
    public decimal HighAsk { get; set; }
    public decimal LowAsk { get; set; }
    public decimal CloseAsk { get; set; }
    
    public int TickCount { get; set; }
}

// ✅ 正しいパターン：OnModelCreatingで事前にクエリを定義
public class RateKsqlContext : KsqlContext
{
    protected override void OnModelCreating(IModelBuilder modelBuilder)
    {
        // 元のレートストリーム
        modelBuilder.Entity<Rate>();
        
        // ✅ 1分足ウィンドウテーブル - 事前にクエリを定義
        modelBuilder.Entity<Rate1MinCandle>()
            .HasQueryFrom<Rate>(q => 
                q.Window(1) // 1分ウィンドウ
                 .GroupBy(r => r.Symbol)
                 .Select(g => new Rate1MinCandle
                 {
                     Symbol = g.Key,
                     WindowStart = g.Window.Start,
                     WindowEnd = g.Window.End,
                     OpenBid = g.OrderBy(r => r.Timestamp).First().Bid,
                     HighBid = g.Max(r => r.Bid),
                     LowBid = g.Min(r => r.Bid),
                     CloseBid = g.OrderByDescending(r => r.Timestamp).First().Bid,
                     OpenAsk = g.OrderBy(r => r.Timestamp).First().Ask,
                     HighAsk = g.Max(r => r.Ask),
                     LowAsk = g.Min(r => r.Ask),
                     CloseAsk = g.OrderByDescending(r => r.Timestamp).First().Ask,
                     TickCount = g.Count()
                 }))
            .WithGroupId("rate-1min-processor");
            
        // ✅ 5分足ウィンドウテーブル - 事前にクエリを定義
        modelBuilder.Entity<Rate5MinCandle>()
            .HasQueryFrom<Rate>(q => 
                q.Window(5) // 5分ウィンドウ
                 .GroupBy(r => r.Symbol)
                 .Select(g => new Rate5MinCandle
                 {
                     Symbol = g.Key,
                     WindowStart = g.Window.Start,
                     WindowEnd = g.Window.End,
                     OpenBid = g.OrderBy(r => r.Timestamp).First().Bid,
                     HighBid = g.Max(r => r.Bid),
                     LowBid = g.Min(r => r.Bid),
                     CloseBid = g.OrderByDescending(r => r.Timestamp).First().Bid,
                     OpenAsk = g.OrderBy(r => r.Timestamp).First().Ask,
                     HighAsk = g.Max(r => r.Ask),
                     LowAsk = g.Min(r => r.Ask),
                     CloseAsk = g.OrderByDescending(r => r.Timestamp).First().Ask,
                     TickCount = g.Count()
                 }))
            .WithGroupId("rate-5min-processor");
            
        // ✅ 10分足ウィンドウテーブル - 事前にクエリを定義
        modelBuilder.Entity<Rate10MinCandle>()
            .HasQueryFrom<Rate>(q => 
                q.Window(10) // 10分ウィンドウ
                 .GroupBy(r => r.Symbol)
                 .Select(g => new Rate10MinCandle
                 {
                     Symbol = g.Key,
                     WindowStart = g.Window.Start,
                     WindowEnd = g.Window.End,
                     OpenBid = g.OrderBy(r => r.Timestamp).First().Bid,
                     HighBid = g.Max(r => r.Bid),
                     LowBid = g.Min(r => r.Bid),
                     CloseBid = g.OrderByDescending(r => r.Timestamp).First().Bid,
                     OpenAsk = g.OrderBy(r => r.Timestamp).First().Ask,
                     HighAsk = g.Max(r => r.Ask),
                     LowAsk = g.Min(r => r.Ask),
                     CloseAsk = g.OrderByDescending(r => r.Timestamp).First().Ask,
                     TickCount = g.Count()
                 }))
            .WithGroupId("rate-10min-processor");
    }
}

// メイン処理クラス
public class RateWindowProcessor
{
    private readonly RateKsqlContext _context;
    
    public RateWindowProcessor()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();

        _context = KsqlContextBuilder.Create()
            .UseConfiguration(configuration)
            .UseSchemaRegistry(configuration["KsqlDsl:SchemaRegistry:Url"]!)
            .EnableLogging(LoggerFactory.Create(builder => builder.AddConsole()))
            .BuildContext<RateKsqlContext>();
    }
    
    // レートデータの送信
    public async Task SendRateAsync(string symbol, decimal bid, decimal ask)
    {
        var rate = new Rate
        {
            Symbol = symbol,
            Bid = bid,
            Ask = ask,
            Timestamp = DateTimeOffset.UtcNow
        };
        
        await _context.Set<Rate>().AddAsync(rate);
        Console.WriteLine($"Sent rate: {symbol} Bid:{bid} Ask:{ask}");
    }
    
    // ✅ 1分足データの処理 - 事前定義されたストリーム/テーブルを使用
    public async Task Process1MinCandles()
    {
        await _context.Set<Rate1MinCandle>()
            .ForEachAsync(candle => 
            {
                Console.WriteLine($"1Min Candle: {candle.Symbol} [{candle.WindowStart:HH:mm:ss}] - " +
                                $"O:{candle.OpenBid:F4} H:{candle.HighBid:F4} L:{candle.LowBid:F4} C:{candle.CloseBid:F4} " +
                                $"Ticks:{candle.TickCount}");
            });
    }
    
    // ✅ 5分足データの処理 - 事前定義されたストリーム/テーブルを使用
    public async Task Process5MinCandles()
    {
        await _context.Set<Rate5MinCandle>()
            .ForEachAsync(candle => 
            {
                Console.WriteLine($"5Min Candle: {candle.Symbol} [{candle.WindowStart:HH:mm:ss}] - " +
                                $"O:{candle.OpenBid:F4} H:{candle.HighBid:F4} L:{candle.LowBid:F4} C:{candle.CloseBid:F4} " +
                                $"Ticks:{candle.TickCount}");
            });
    }
    
    // ✅ 10分足データの処理 - 事前定義されたストリーム/テーブルを使用
    public async Task Process10MinCandles()
    {
        await _context.Set<Rate10MinCandle>()
            .ForEachAsync(candle => 
            {
                Console.WriteLine($"10Min Candle: {candle.Symbol} [{candle.WindowStart:HH:mm:ss}] - " +
                                $"O:{candle.OpenBid:F4} H:{candle.HighBid:F4} L:{candle.LowBid:F4} C:{candle.CloseBid:F4} " +
                                $"Ticks:{candle.TickCount}");
            });
    }
    
    // Finalトピックからの確定データ利用
    public async Task ProcessFinalizedCandles()
    {
        // 1分足の確定データを利用
        await _context.Set<Rate1MinCandle>()
            .UseFinalTopic() // 確定されたデータのみ使用
            .ForEachAsync(candle =>
            {
                Console.WriteLine($"Finalized 1Min: {candle.Symbol} at {candle.WindowStart:HH:mm:ss} " +
                                $"Close:{candle.CloseBid:F4}");
            });
    }
}

// 使用例
class Program
{
    static async Task Main(string[] args)
    {
        var processor = new RateWindowProcessor();
        
        // レートデータ送信タスク
        var sendTask = Task.Run(async () =>
        {
            var symbols = new[] { "USDJPY", "EURJPY", "GBPJPY" };
            var random = new Random();
            
            for (int i = 0; i < 100; i++)
            {
                foreach (var symbol in symbols)
                {
                    var bid = 110m + (decimal)(random.NextDouble() * 10);
                    var ask = bid + 0.003m;
                    
                    await processor.SendRateAsync(symbol, bid, ask);
                }
                
                await Task.Delay(1000); // 1秒間隔
            }
        });
        
        // ウィンドウ処理タスク
        var process1MinTask = processor.Process1MinCandles();
        var process5MinTask = processor.Process5MinCandles();
        var process10MinTask = processor.Process10MinCandles();
        
        // 並列実行
        await Task.WhenAny(sendTask, process1MinTask, process5MinTask, process10MinTask);
    }
}

// appsettings.json 設定例
/*
{
  "KsqlDsl": {
    "ValidationMode": "Strict",
    "SchemaRegistry": {
      "Url": "http://localhost:8081",
      "AutoRegisterSchemas": true
    },
    "Topics": {
      "rates": {
        "NumPartitions": 3,
        "ReplicationFactor": 1
      },
      "rate_candles_1min": {
        "NumPartitions": 3,
        "ReplicationFactor": 1
      },
      "rate_candles_5min": {
        "NumPartitions": 3,
        "ReplicationFactor": 1
      },
      "rate_candles_10min": {
        "NumPartitions": 3,
        "ReplicationFactor": 1
      }
    },
    "Entities": [
      {
        "Entity": "Rate",
        "SourceTopic": "rates",
        "Windows": [1, 5, 10]
      }
    ]
  },
  "Kafka": {
    "BootstrapServers": "localhost:9092"
  }
}
*/