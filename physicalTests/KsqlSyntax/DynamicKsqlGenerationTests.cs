using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Core.Modeling;
using Kafka.Ksql.Linq.Query.Pipeline;
using Kafka.Ksql.Linq.Query.Abstractions;
using Kafka.Ksql.Linq.Application;
using Kafka.Ksql.Linq.Core.Context;
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

    private static IEnumerable<string> GenerateDmlQueries(Dictionary<Type, EntityModel> models)
    {
        var dml = new DMLQueryGenerator();
        foreach (var model in models.Values)
        {
            var name = model.TopicName ?? model.EntityType.Name.ToLowerInvariant();
            yield return ExecuteInScope(() => dml.GenerateSelectAll(name, false).ToUpperInvariant());

            if (model.StreamTableType == StreamTableType.Table)
            {
                var param = Expression.Parameter(model.EntityType, "e");
                var prop = Expression.Property(param, model.AllProperties.First());
                var constType = prop.Type;
                var zero = Activator.CreateInstance(Nullable.GetUnderlyingType(constType) ?? constType);
                var const1 = Expression.Constant(zero, constType);
                var body = Expression.GreaterThan(prop, const1);
                var lambda = Expression.Lambda(body, param);
                yield return ExecuteInScope(() => dml.GenerateSelectWithCondition(name, lambda.Body, true).ToUpperInvariant());
                yield return ExecuteInScope(() => dml.GenerateCountQuery(name).ToUpperInvariant());
            }
        }

        IQueryable<OrderValue> orders = new List<OrderValue>().AsQueryable();

        yield return ExecuteInScope(() => dml.GenerateAggregateQuery("orders", ((Expression<Func<IGrouping<int, OrderValue>, object>>)(g => new { Sum = g.Sum(x => (double)x.Amount) })).Body).ToUpperInvariant());
        yield return ExecuteInScope(() => dml.GenerateAggregateQuery("orders", ((Expression<Func<IGrouping<int, OrderValue>, object>>)(g => new { Last = g.LatestByOffset(x => x.Id) })).Body).ToUpperInvariant());
        yield return ExecuteInScope(() => dml.GenerateAggregateQuery("orders", ((Expression<Func<IGrouping<int, OrderValue>, object>>)(g => new { First = g.EarliestByOffset(x => x.Id) })).Body).ToUpperInvariant());

        var complex = orders
            .Where(o => o.Amount > 100)
            .Window(TumblingWindow.OfMinutes(5))
            .GroupBy(o => o.CustomerId)
            .Having(g => g.Count() > 1)
            .Select(g => new { g.Key, Count = g.Count() });
        yield return ExecuteInScope(() => dml.GenerateLinqQuery("orders", complex.Expression, false).ToUpperInvariant());

        IQueryable<Customer> customers = new List<Customer>().AsQueryable();
        var join = orders
            .Join(customers, o => o.CustomerId, c => c.Id, (o, c) => new { o, c })
            .GroupBy(x => x.o.CustomerId)
            .Having(g => g.Sum(x => (double)x.o.Amount) > 1000)
            .Select(g => new { g.Key, Total = g.Sum(x => (double)x.o.Amount) });
        yield return ExecuteInScope(() => dml.GenerateLinqQuery("orders", join.Expression, false).ToUpperInvariant());

        var multiKey = orders
            .GroupBy(o => new { o.CustomerId, o.Region })
            .Having(g => g.Sum(x => (double)x.Amount) > 500)
            .Select(g => new { g.Key.CustomerId, g.Key.Region, Total = g.Sum(x => (double)x.Amount) });
        yield return ExecuteInScope(() => dml.GenerateLinqQuery("orders", multiKey.Expression, false).ToUpperInvariant());

        var conditionalSum = orders
            .GroupBy(o => o.CustomerId)
            .Select(g => new
            {
                g.Key,
                Total = g.Sum(o => (double)o.Amount),
                HighPriorityTotal = g.Sum(o => o.IsHighPriority ? (double)o.Amount : 0d)
            });
        yield return ExecuteInScope(() => dml.GenerateLinqQuery("orders", conditionalSum.Expression, false).ToUpperInvariant());

        var avgMinMax = orders
            .GroupBy(o => o.CustomerId)
            .Select(g => new
            {
                g.Key,
                AverageAmount = g.Average(o => (double)o.Amount),
                MinAmount = g.Min(o => o.Amount),
                MaxAmount = g.Max(o => o.Amount)
            });
        yield return ExecuteInScope(() => dml.GenerateLinqQuery("orders", avgMinMax.Expression, false).ToUpperInvariant());

        var orderByDesc = orders
            .GroupBy(o => o.CustomerId)
            .Select(g => new { g.Key, Total = g.Sum(o => (double)o.Amount) });
        yield return ExecuteInScope(() => dml.GenerateLinqQuery("orders", orderByDesc.Expression, false).ToUpperInvariant());

        var orderByThenBy = orders
            .GroupBy(o => new { o.CustomerId, o.Region })
            .Select(g => new { g.Key.CustomerId, g.Key.Region, Total = g.Sum(o => (double)o.Amount) });
        yield return ExecuteInScope(() => dml.GenerateLinqQuery("orders", orderByThenBy.Expression, false).ToUpperInvariant());

        var complexHaving = orders
            .GroupBy(o => new { o.CustomerId, o.Region })
            .Having(g => (g.Sum(x => (double)x.Amount) > 1000 && g.Count() > 10) || g.Average(x => (double)x.Amount) > 150)
            .Select(g => new
            {
                g.Key.CustomerId,
                g.Key.Region,
                TotalAmount = g.Sum(x => (double)x.Amount),
                OrderCount = g.Count(),
                AverageAmount = g.Average(x => (double)x.Amount)
            });
        yield return ExecuteInScope(() => dml.GenerateLinqQuery("orders", complexHaving.Expression, false).ToUpperInvariant());

        var caseWhen = orders
            .GroupBy(o => o.CustomerId)
            .Select(g => new
            {
                g.Key,
                Total = g.Sum(o => (double)o.Amount),
                Status = g.Sum(o => (double)o.Amount) > 1000 ? "VIP" : "Regular"
            });
        yield return ExecuteInScope(() => dml.GenerateLinqQuery("orders", caseWhen.Expression, false).ToUpperInvariant());

        var groupWhereHaving = orders
            .GroupBy(o => o.CustomerId)
            .Where(g => (g.Sum(o => (double)o.Amount) > 1000 && g.Count() > 5) || g.Average(o => (double)o.Amount) > 500)
            .Select(g => new
            {
                g.Key,
                Total = g.Sum(o => (double)o.Amount),
                Count = g.Count(),
                Avg = g.Average(o => (double)o.Amount)
            });
        yield return ExecuteInScope(() => dml.GenerateLinqQuery("orders", groupWhereHaving.Expression, false).ToUpperInvariant());

        var orHaving = orders
            .GroupBy(o => o.CustomerId)
            .Where(g => g.Sum(x => (double)x.Amount) > 1000 || g.Sum(x => x.Count) > 5)
            .Select(g => new { g.Key, TotalAmount = g.Sum(x => (double)x.Amount), TotalCount = g.Sum(x => x.Count) });
        yield return ExecuteInScope(() => dml.GenerateLinqQuery("orders", orHaving.Expression, false).ToUpperInvariant());

        var excluded = new[] { "CN", "RU" };
        var notInQuery = orders
            .Where(o => !excluded.Contains(o.Region))
            .Select(o => new { o.CustomerId, o.Region, o.Amount });
        yield return ExecuteInScope(() => dml.GenerateLinqQuery("orders", notInQuery.Expression, false).ToUpperInvariant());

        IQueryable<NullableOrder> nullOrders = new List<NullableOrder>().AsQueryable();
        var isNullQuery = nullOrders
            .Where(o => o.CustomerId == null)
            .Select(o => new { o.Region, o.Amount });
        yield return ExecuteInScope(() => dml.GenerateLinqQuery("orders_nullable", isNullQuery.Expression, false).ToUpperInvariant());

        var isNotNullQuery = nullOrders
            .Where(o => o.CustomerId != null)
            .Select(o => new { o.Region, o.Amount });
        yield return ExecuteInScope(() => dml.GenerateLinqQuery("orders_nullable", isNotNullQuery.Expression, false).ToUpperInvariant());

        IQueryable<NullableKeyOrder> nullKeyOrders = new List<NullableKeyOrder>().AsQueryable();
        var groupNullableKey = nullKeyOrders
            .Where(o => o.CustomerId != null)
            .GroupBy(o => o.CustomerId)
            .Select(g => new { CustomerId = g.Key, Total = g.Sum(x => (double)x.Amount) });
        yield return ExecuteInScope(() => dml.GenerateLinqQuery("orders_nullable_key", groupNullableKey.Expression, false).ToUpperInvariant());

        var exprKey = orders
            .GroupBy(o => o.Region.ToUpper())
            .Having(g => g.Sum(x => (double)x.Amount) > 500)
            .Select(g => new { RegionUpper = g.Key, TotalAmount = g.Sum(x => (double)x.Amount) });
        yield return ExecuteInScope(() => dml.GenerateLinqQuery("orders", exprKey.Expression, false).ToUpperInvariant());
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
            foreach (var dml in GenerateDmlQueries(models))
            {
                var response = await ctx.ExecuteExplainAsync(dml);
                Assert.True(response.IsSuccess, $"{dml} failed: {response.Message}");
            }
        }
    }

    public static IEnumerable<object[]> AllDmlQueries()
    {
        var models = BuildModels();
        var queries = GenerateDmlQueries(models).ToList();
        if (queries.Count == 0)
            yield return new object[] { "SELECT 1;" };
        else
            foreach (var q in queries)
                yield return new object[] { q };
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
        public DummyContext() : base() { }
        public DummyContext(KafkaContextOptions options) : base(options) { }
        protected override void OnModelCreating(IModelBuilder modelBuilder)
        {
            DynamicKsqlGenerationTests.ConfigureModel(modelBuilder);
        }
    }

    private async Task ProduceDummyRecordsAsync()
    {
        var options = new KafkaContextOptions
        {
            BootstrapServers = "localhost:9092",
            SchemaRegistryUrl = "http://localhost:8088"
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
