using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Core.Modeling;
using Kafka.Ksql.Linq.Query.Pipeline;
using Kafka.Ksql.Linq.Query.Abstractions;
using Kafka.Ksql.Linq.Application;
using Kafka.Ksql.Linq;
using Kafka.Ksql.Linq.Configuration;
using Kafka.Ksql.Linq.Core.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Integration;

public class DynamicKsqlGenerationTests
{

    private static void ConfigureModel(IModelBuilder builder)
    {
        builder.Entity<OrderValue>().WithTopic("orders");
        builder.Entity<Customer>().WithTopic("customers");
        builder.Entity<EventLog>().WithTopic("events");
        builder.Entity<NullableOrder>().WithTopic("orders_nullable");
        builder.Entity<NullableKeyOrder>().WithTopic("orders_nullable_key");
    }

    private static Dictionary<Type, EntityModel> BuildModels()
    {
        var mb = new ModelBuilder();
        using (ModelCreatingScope.Enter())
        {
            ConfigureModel(mb);
        }
        mb.ValidateAllModels();
        return mb.GetAllEntityModels();
    }

    private static T ExecuteInScope<T>(Func<T> func)
    {
        using (ModelCreatingScope.Enter())
        {
            return func();
        }
    }

    private static IEnumerable<string> GenerateDdlQueries(Dictionary<Type, EntityModel> models)
    {
        var ddl = new DDLQueryGenerator();
        foreach (var model in models.Values)
        {
            var name = model.TopicName ?? model.EntityType.Name.ToLowerInvariant();
            if (model.StreamTableType == StreamTableType.Table)
                yield return ExecuteInScope(() => ddl.GenerateCreateTable(name, name, model));
            else
                yield return ExecuteInScope(() => ddl.GenerateCreateStream(name, name, model));
        }

        IQueryable<OrderValue> orders = new List<OrderValue>().AsQueryable();
        var tableExpr = orders
            .Where(o => o.Amount > 100)
            .GroupBy(o => o.Region)
            .Select(g => new { g.Key, Total = g.Sum(x => (double)x.Amount) });
        yield return ExecuteInScope(() => ddl.GenerateCreateTableAs("orders_by_region", "orders", tableExpr.Expression));

        IQueryable<Customer> customers = new List<Customer>().AsQueryable();
        var streamExpr = orders
            .Join(customers, o => o.CustomerId, c => c.Id, (o, c) => new { o, c })
            .Select(x => new { x.o.CustomerId, x.c.Name, x.o.Amount });
        yield return ExecuteInScope(() => ddl.GenerateCreateStreamAs("order_enriched", "orders", streamExpr.Expression));
    }

    private static IEnumerable<(string Description, string Ksql)> GenerateDmlQueries()
    {
        using var ctx = new DummyContext(new KsqlDslOptions());

        yield return ("SelectAll_Orders", ctx.Entity<OrderValue>().ToQueryString());
        yield return ("SelectAll_Customers", ctx.Entity<Customer>().ToQueryString());
        yield return ("SelectAll_Events", ctx.Entity<EventLog>().ToQueryString());
        yield return ("SelectAll_NullableOrder", ctx.Entity<NullableOrder>().ToQueryString());
        yield return ("SelectAll_NullableKeyOrder", ctx.Entity<NullableKeyOrder>().ToQueryString());

        yield return ("Aggregate_Sum", ctx.Entity<OrderValue>()
            .GroupBy(o => o.CustomerId)
            .Select(g => new { g.Key, Sum = g.Sum(x => (double)x.Amount) })
            .ToQueryString());

        yield return ("Aggregate_Latest", ctx.Entity<OrderValue>()
            .GroupBy(o => o.CustomerId)
            .Select(g => new { g.Key, Last = g.LatestByOffset(x => x.Id) })
            .ToQueryString());

        yield return ("Aggregate_First", ctx.Entity<OrderValue>()
            .GroupBy(o => o.CustomerId)
            .Select(g => new { g.Key, First = g.EarliestByOffset(x => x.Id) })
            .ToQueryString());

        yield return ("Complex_Window", ctx.Entity<OrderValue>()
            .Where(o => o.Amount > 100)
            .Window(TumblingWindow.OfMinutes(5))
            .GroupBy(o => o.CustomerId)
            .Having(g => g.Count() > 1)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToQueryString());

        yield return ("Join_Having", ctx.Entity<OrderValue>()
            .Join(ctx.Entity<Customer>(), o => o.CustomerId, c => c.Id, (o, c) => new { o, c })
            .GroupBy(x => x.o.CustomerId)
            .Having(g => g.Sum(x => (double)x.o.Amount) > 1000)
            .Select(g => new { g.Key, Total = g.Sum(x => (double)x.o.Amount) })
            .ToQueryString());

        yield return ("GroupBy_MultiKey", ctx.Entity<OrderValue>()
            .GroupBy(o => new { o.CustomerId, o.Region })
            .Having(g => g.Sum(x => (double)x.Amount) > 500)
            .Select(g => new { g.Key.CustomerId, g.Key.Region, Total = g.Sum(x => (double)x.Amount) })
            .ToQueryString());

        yield return ("Conditional_Sum", ctx.Entity<OrderValue>()
            .GroupBy(o => o.CustomerId)
            .Select(g => new
            {
                g.Key,
                Total = g.Sum(o => (double)o.Amount),
                HighPriorityTotal = g.Sum(o => o.IsHighPriority ? (double)o.Amount : 0d)
            })
            .ToQueryString());

        yield return ("Aggregate_AvgMinMax", ctx.Entity<OrderValue>()
            .GroupBy(o => o.CustomerId)
            .Select(g => new
            {
                g.Key,
                AverageAmount = g.Average(o => (double)o.Amount),
                MinAmount = g.Min(o => o.Amount),
                MaxAmount = g.Max(o => o.Amount)
            })
            .ToQueryString());

        yield return ("OrderByDesc", ctx.Entity<OrderValue>()
            .GroupBy(o => o.CustomerId)
            .Select(g => new { g.Key, Total = g.Sum(o => (double)o.Amount) })
            .ToQueryString());

        yield return ("OrderByThenBy", ctx.Entity<OrderValue>()
            .GroupBy(o => new { o.CustomerId, o.Region })
            .Select(g => new { g.Key.CustomerId, g.Key.Region, Total = g.Sum(o => (double)o.Amount) })
            .ToQueryString());

        yield return ("Complex_Having", ctx.Entity<OrderValue>()
            .GroupBy(o => new { o.CustomerId, o.Region })
            .Having(g => (g.Sum(x => (double)x.Amount) > 1000 && g.Count() > 10) || g.Average(x => (double)x.Amount) > 150)
            .Select(g => new
            {
                g.Key.CustomerId,
                g.Key.Region,
                TotalAmount = g.Sum(x => (double)x.Amount),
                OrderCount = g.Count(),
                AverageAmount = g.Average(x => (double)x.Amount)
            })
            .ToQueryString());

        yield return ("Case_When", ctx.Entity<OrderValue>()
            .GroupBy(o => o.CustomerId)
            .Select(g => new
            {
                g.Key,
                Total = g.Sum(o => (double)o.Amount),
                Status = g.Sum(o => (double)o.Amount) > 1000 ? "VIP" : "Regular"
            })
            .ToQueryString());

        yield return ("GroupWhereHaving", ctx.Entity<OrderValue>()
            .GroupBy(o => o.CustomerId)
            .Where(g => (g.Sum(o => (double)o.Amount) > 1000 && g.Count() > 5) || g.Average(o => (double)o.Amount) > 500)
            .Select(g => new
            {
                g.Key,
                Total = g.Sum(o => (double)o.Amount),
                Count = g.Count(),
                Avg = g.Average(o => (double)o.Amount)
            })
            .ToQueryString());

        yield return ("Or_Having", ctx.Entity<OrderValue>()
            .GroupBy(o => o.CustomerId)
            .Where(g => g.Sum(x => (double)x.Amount) > 1000 || g.Sum(x => x.Count) > 5)
            .Select(g => new { g.Key, TotalAmount = g.Sum(x => (double)x.Amount), TotalCount = g.Sum(x => x.Count) })
            .ToQueryString());

        var excluded = new[] { "CN", "RU" };
        yield return ("Not_In", ctx.Entity<OrderValue>()
            .Where(o => !excluded.Contains(o.Region))
            .Select(o => new { o.CustomerId, o.Region, o.Amount })
            .ToQueryString());

        yield return ("IsNull", ctx.Entity<NullableOrder>()
            .Where(o => o.CustomerId == null)
            .Select(o => new { o.Region, o.Amount })
            .ToQueryString());

        yield return ("IsNotNull", ctx.Entity<NullableOrder>()
            .Where(o => o.CustomerId != null)
            .Select(o => new { o.Region, o.Amount })
            .ToQueryString());

        yield return ("Group_NullableKey", ctx.Entity<NullableKeyOrder>()
            .Where(o => o.CustomerId != null)
            .GroupBy(o => o.CustomerId)
            .Select(g => new { CustomerId = g.Key, Total = g.Sum(x => (double)x.Amount) })
            .ToQueryString());

        yield return ("Expr_Key", ctx.Entity<OrderValue>()
            .GroupBy(o => o.Region.ToUpper())
            .Having(g => g.Sum(x => (double)x.Amount) > 500)
            .Select(g => new { RegionUpper = g.Key, TotalAmount = g.Sum(x => (double)x.Amount) })
            .ToQueryString());
    }

    // OnModelCreating で生成したモデルから DDL/DML が正しく実行できるか検証
    [KsqlDbFact]
    [Trait("Category", "Integration")]
    public async Task CreateAllObjectsByOnModelCreating()
    {
        await TestEnvironment.ResetAsync();

        var models = BuildModels();
        var ddls = GenerateDdlQueries(models).ToList();

        // drop all objects first
        await using (var ctx = TestEnvironment.CreateContext())
        {
            foreach (var ddl in ddls)
            {
                var drop = ddl.StartsWith("CREATE STREAM")
                    ? ddl.Replace("CREATE STREAM", "DROP STREAM IF EXISTS") + " DELETE TOPIC;"
                    : ddl.Replace("CREATE TABLE", "DROP TABLE IF EXISTS") + " DELETE TOPIC;";
                await ctx.ExecuteStatementAsync(drop);
            }

            // then create all objects
            foreach (var ddl in ddls)
            {
                var result = await ctx.ExecuteStatementAsync(ddl);
                var success = result.IsSuccess ||
                    (result.Message?.Contains("already exists", StringComparison.OrdinalIgnoreCase) ?? false);
                Assert.True(success, $"DDL failed: {result.Message}");
            }
        }

        // insert dummy records so ksqlDB can materialize schemas
        await ProduceDummyRecordsAsync();

        // validate that all DML queries are executable
        await using (var ctx = TestEnvironment.CreateContext())
        {
            foreach (var (_, ksql) in GenerateDmlQueries())
            {
                var response = await ctx.ExecuteExplainAsync(ksql);
                Assert.True(response.IsSuccess, $"{ksql} failed: {response.Message}");
            }
        }
    }

    public static IEnumerable<object[]> AllDmlQueries()
    {
        var queries = GenerateDmlQueries().ToList();
        if (queries.Count == 0)
            yield return new object[] { "SELECT 1;" };
        else
            foreach (var q in queries)
                yield return new object[] { q.Ksql };
    }

    // 生成したすべてのDMLクエリがksqlDBで有効か確認
    [KsqlDbTheory]
    [Trait("Category", "Integration")]
    [MemberData(nameof(AllDmlQueries), MemberType = typeof(DynamicKsqlGenerationTests))]
    public async Task AllDmlQueries_ShouldBeValidInKsqlDb(string ksql)
    {
        await TestEnvironment.ResetAsync();

        if (!TestSchema.IsSupportedKsql(ksql))
            return; // skip unsupported queries

        TestSchema.ValidateDmlQuery(ksql);
        await using var ctx = TestEnvironment.CreateContext();
        var response = await ctx.ExecuteExplainAsync(ksql);
        Assert.True(response.IsSuccess, $"{ksql} failed: {response.Message}");
    }

    // sample entities
    public class OrderValue
    {
        public int CustomerId { get; set; }
        public int Id { get; set; }
        public string Region { get; set; } = string.Empty;
        public double Amount { get; set; }
        public bool IsHighPriority { get; set; }
        public int Count { get; set; }
    }

    public class Customer
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class EventLog
    {
        public int Level { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class NullableOrder
    {
        public int? CustomerId { get; set; }
        public string Region { get; set; } = string.Empty;
        public double Amount { get; set; }
    }

    public class NullableKeyOrder
    {
        public int? CustomerId { get; set; }
        public double Amount { get; set; }
    }

    public class DummyContext : KsqlContext
    {
        public DummyContext() : base(new KsqlDslOptions()) { }
        public DummyContext(KsqlDslOptions options) : base(options) { }
        protected override void OnModelCreating(IModelBuilder modelBuilder)
        {
            DynamicKsqlGenerationTests.ConfigureModel(modelBuilder);
        }
    }

    private async Task ProduceDummyRecordsAsync()
    {
        var options = new KsqlDslOptions
        {
            Common = new CommonSection { BootstrapServers = TestEnvironment.KafkaBootstrapServers },
            SchemaRegistry = new SchemaRegistrySection { Url = TestEnvironment.SchemaRegistryUrl }
        };

        await using var ctx = new DummyContext(options);

        await ctx.Set<OrderValue>().AddAsync(new OrderValue
        {
            CustomerId = 1,
            Id = 1,
            Region = "east",
            Amount = 10d,
            IsHighPriority = false,
            Count = 1
        });

        await ctx.Set<Customer>().AddAsync(new Customer { Id = 1, Name = "alice" });
        await ctx.Set<EventLog>().AddAsync(new EventLog { Level = 1, Message = "init" });
        await ctx.Set<NullableOrder>().AddAsync(new NullableOrder { CustomerId = 1, Region = "east", Amount = 10d });
        await ctx.Set<NullableKeyOrder>().AddAsync(new NullableKeyOrder { CustomerId = 1, Amount = 10d });

        await Task.Delay(500);
        await ctx.DisposeAsync();
    }
}
