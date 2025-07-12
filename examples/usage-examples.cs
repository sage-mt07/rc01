using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Core.Modeling;
using System;
using System.Linq;

// ===== 使用例 =====

/// <summary>
/// サンプルエンティティ（ソース）
/// </summary>
[Topic("orders")]
public class OrderEntity
{
    public int OrderId { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime OrderDate { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// サンプルエンティティ（ターゲット・集約結果）
/// </summary>
[Topic("customer_order_summary")]
public class CustomerOrderSummary
{
    public string CustomerId { get; set; } = string.Empty; // GroupByキー
    public int OrderCount { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime LastOrderDate { get; set; }
}

/// <summary>
/// KsqlContextでのOnModelCreating使用例
/// </summary>
public class SampleKsqlContext : KsqlContext
{
    protected override void OnModelCreating(IModelBuilder modelBuilder)
    {
        // === 基本的なHasQuery使用法 ===
        
        // 1. シンプルなGroupBy + Select
        modelBuilder.Entity<CustomerOrderSummary>()
            .HasQuery<OrderEntity>(orders => orders
                .GroupBy(o => o.CustomerId)
                .Select(g => new CustomerOrderSummary
                {
                    CustomerId = g.Key,                    // ← 自動的にKeyとして認識
                    OrderCount = g.Count(),
                    TotalAmount = g.Sum(o => o.Amount),
                    LastOrderDate = g.Max(o => o.OrderDate)
                }))
            .AsTable("customer_summary");

        // 2. 複合キーの例
        modelBuilder.Entity<DailySalesEntity>()
            .HasQuery<OrderEntity>(orders => orders
                .GroupBy(o => new { o.CustomerId, Date = o.OrderDate.Date })
                .Select(g => new DailySalesEntity
                {
                    CustomerId = g.Key.CustomerId,        // ← 複合キーの一部
                    Date = g.Key.Date,                     // ← 複合キーの一部
                    DailySales = g.Sum(o => o.Amount)
                }))
            .AsTable();

        // 3. キーなし（Stream型）の例
        modelBuilder.Entity<OrderEventEntity>()
            .HasQuery<OrderEntity>(orders => orders
                .Where(o => o.Status == "COMPLETED")
                .Select(o => new OrderEventEntity
                {
                    OrderId = o.OrderId,
                    EventType = "ORDER_COMPLETED",
                    Timestamp = DateTime.UtcNow
                }))
            .AsStream("order_events");

        // 4. QueryBuilderを使った詳細設定
        modelBuilder.Entity<TopCustomerEntity>()
            .HasQuery<CustomerOrderSummary>(builder => builder
                .FromSource<CustomerOrderSummary>(summaries => summaries
                    .Where(s => s.TotalAmount > 10000)
                    .OrderByDescending(s => s.TotalAmount)
                    .Take(100))
                .WithKeyExtraction(auto: true)
                .AsTable("top_customers"));

        // === DefineQuery使用法（代替記法） ===
        
        // 5. 直接的なクエリ定義
        modelBuilder.DefineQuery<OrderEntity, CustomerOrderSummary>(orders => orders
            .GroupBy(o => o.CustomerId)
            .Select(g => new CustomerOrderSummary
            {
                CustomerId = g.Key,
                OrderCount = g.Count(),
                TotalAmount = g.Sum(o => o.Amount),
                LastOrderDate = g.Max(o => o.OrderDate)
            }));

        // 6. 既存エンティティの拡張
        modelBuilder.Entity<OrderEntity>()
            .HasKey(o => o.OrderId)
            .AsTable("orders", useCache: true);
    }

    /// <summary>
    /// 初期化完了後のQuerySchema登録
    /// </summary>
    protected override void ConfigureModel()
    {
        base.ConfigureModel();
        
        // QuerySchemaの自動登録
        this.RegisterQuerySchemas();
        
        // 登録されたスキーマの確認（デバッグ用）
        var schemas = this.GetAllQuerySchemas();
        foreach (var (type, schema) in schemas)
        {
            Console.WriteLine($"[QuerySchema] {QuerySchemaHelper.GetSchemaSummary(schema)}");
        }
    }
}

/// <summary>
/// 追加のサンプルエンティティ
/// </summary>
[Topic("daily_sales")]
public class DailySalesEntity
{
    public string CustomerId { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public decimal DailySales { get; set; }
}

[Topic("order_events")]
public class OrderEventEntity
{
    public int OrderId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

[Topic("top_customers")]
public class TopCustomerEntity
{
    public string CustomerId { get; set; } = string.Empty;
    public int OrderCount { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime LastOrderDate { get; set; }
}

// ===== 実用例：複雑なクエリパターン =====

/// <summary>
/// 高度な使用例を含むKsqlContext
/// </summary>
public class AdvancedKsqlContext : KsqlContext
{
    protected override void OnModelCreating(IModelBuilder modelBuilder)
    {
        // === ウィンドウ集約（将来対応） ===
        // modelBuilder.Entity<HourlyOrderStats>()
        //     .HasQuery<OrderEntity>(orders => orders
        //         .Window(TimeSpan.FromHours(1))
        //         .GroupBy(o => o.CustomerId)
        //         .Select(g => new HourlyOrderStats { ... }))
        //     .AsTable();

        // === JOIN（将来対応） ===
        // modelBuilder.Entity<OrderWithCustomer>()
        //     .HasQuery<OrderEntity>(orders => orders
        //         .Join(customers, o => o.CustomerId, c => c.CustomerId, 
        //               (o, c) => new OrderWithCustomer { ... }))
        //     .AsTable();

        // === 現在サポートされるパターン ===
        
        // 1. 単純集約
        modelBuilder.Entity<OrderCountByStatus>()
            .HasQuery<OrderEntity>(orders => orders
                .GroupBy(o => o.Status)
                .Select(g => new OrderCountByStatus
                {
                    Status = g.Key,
                    Count = g.Count()
                }))
            .AsTable();

        // 2. フィルタ + 集約
        modelBuilder.Entity<HighValueOrderSummary>()
            .HasQuery<OrderEntity>(orders => orders
                .Where(o => o.Amount > 1000)
                .GroupBy(o => o.CustomerId)
                .Select(g => new HighValueOrderSummary
                {
                    CustomerId = g.Key,
                    HighValueOrderCount = g.Count(),
                    HighValueTotal = g.Sum(o => o.Amount)
                }))
            .AsTable();

        // 3. 複雑な計算フィールド
        modelBuilder.Entity<CustomerMetrics>()
            .HasQuery<OrderEntity>(orders => orders
                .GroupBy(o => o.CustomerId)
                .Select(g => new CustomerMetrics
                {
                    CustomerId = g.Key,
                    OrderCount = g.Count(),
                    TotalAmount = g.Sum(o => o.Amount),
                    AverageOrderValue = g.Average(o => o.Amount),
                    FirstOrderDate = g.Min(o => o.OrderDate),
                    LastOrderDate = g.Max(o => o.OrderDate)
                }))
            .AsTable("customer_metrics");
    }
}

/// <summary>
/// 追加エンティティ定義
/// </summary>
[Topic("order_count_by_status")]
public class OrderCountByStatus
{
    public string Status { get; set; } = string.Empty;
    public int Count { get; set; }
}

[Topic("high_value_order_summary")]
public class HighValueOrderSummary
{
    public string CustomerId { get; set; } = string.Empty;
    public int HighValueOrderCount { get; set; }
    public decimal HighValueTotal { get; set; }
}

[Topic("customer_metrics")]
public class CustomerMetrics
{
    public string CustomerId { get; set; } = string.Empty;
    public int OrderCount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AverageOrderValue { get; set; }
    public DateTime FirstOrderDate { get; set; }
    public DateTime LastOrderDate { get; set; }
}

// ===== エラーハンドリング例 =====

/// <summary>
/// 不正なクエリ例（コンパイルエラーまたは実行時エラー）
/// </summary>
public class ErrorHandlingExamples
{
    public void InvalidQueryExamples(IModelBuilder modelBuilder)
    {
        try
        {
            // エラー例1: GroupByなしでのキー推定失敗
            // modelBuilder.Entity<InvalidEntity>()
            //     .HasQuery<OrderEntity>(orders => orders
            //         .Select(o => new InvalidEntity { SomeField = o.Amount }))
            //     .AsTable(); // ← キーが推定できずエラー

            // エラー例2: サポートされないキー型
            // modelBuilder.Entity<BadKeyEntity>()
            //     .HasQuery<OrderEntity>(orders => orders
            //         .GroupBy(o => o.OrderDate) // DateTime型キーは現在未サポート
            //         .Select(g => new BadKeyEntity { Date = g.Key }))
            //     .AsTable();

            // エラー例3: 複雑すぎるクエリ
            // modelBuilder.Entity<ComplexEntity>()
            //     .HasQuery<OrderEntity>(orders => orders
            //         .GroupBy(o => SomeComplexMethod(o)) // メソッド呼び出しは解析不可
            //         .Select(g => new ComplexEntity { ... }))
            //     .AsTable();
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"Query configuration error: {ex.Message}");
            // 適切なエラーハンドリング
        }
    }
}