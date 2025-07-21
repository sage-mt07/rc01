using Confluent.SchemaRegistry;
using Kafka.Ksql.Linq.Application;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq;
using Kafka.Ksql.Linq.Configuration;
using Kafka.Ksql.Linq.Core.Configuration;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Integration;

public class SchemaNameCaseSensitivityTests
{

    public class OrderCorrectCase
    {
        public int CustomerId { get; set; }
        public int Id { get; set; }
        public string Region { get; set; } = string.Empty;
        public double Amount { get; set; }
    }

    public class OrderWrongCase
    {
        public int CustomerId { get; set; }
        public int Id { get; set; }
        public string region { get; set; } = string.Empty; // lowercase r
        public double Amount { get; set; }
    }

    public class OrderContext : KsqlContext
    {
        public OrderContext() : base(new KsqlDslOptions()) { }
        public OrderContext(KsqlDslOptions options) : base(options) { }
        protected override void OnModelCreating(IModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OrderCorrectCase>().WithTopic("orders");
        }
    }

    // Context for OrderWrongCase using default serialization
    public class WrongCaseContext : KsqlContext
    {
        public WrongCaseContext() : base(new KsqlDslOptions()) { }
        public WrongCaseContext(KsqlDslOptions options) : base(options) { }
        protected override void OnModelCreating(IModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OrderWrongCase>().WithTopic("orders");
        }
    }

    private async Task EnsureTablesAsync()
    {
        await using var ctx = TestEnvironment.CreateContext();
        foreach (var ddl in TestSchema.GenerateTableDdls())
            await ctx.ExecuteStatementAsync(ddl);
    }

    private async Task ProduceValidDummyAsync()
    {
        var options = new KsqlDslOptions
        {
            Common = new CommonSection { BootstrapServers = "localhost:9092" },
            SchemaRegistry = new SchemaRegistrySection { Url = "http://localhost:8088" }
        };

        await using var ctx = new OrderContext(options);

        var headers = new Dictionary<string, string> { ["is_dummy"] = "true" };

        await ctx.Set<OrderCorrectCase>().AddAsync(new OrderCorrectCase
        {
            CustomerId = 1,
            Id = 1,
            Region = "east",
            Amount = 10d
        }, headers);

        await Task.Delay(500);
        await ctx.DisposeAsync();
    }

    // スキーマ定義と異なるフィールド名の大文字小文字違いを送信した場合に例外が発生するか確認
    [KsqlDbFact]
    [Trait("Category", "Integration")]
    public async Task MismatchedFieldCase_ShouldThrowException()
    {
        await TestEnvironment.ResetAsync();

        await EnsureTablesAsync();
        await ProduceValidDummyAsync();

        var verifyOptions = new KsqlDslOptions
        {
            Common = new CommonSection { BootstrapServers = "localhost:9092" },
            SchemaRegistry = new SchemaRegistrySection { Url = "http://localhost:8088" }
        };

        await using var verifyCtx = new OrderContext(verifyOptions);

        var list = await verifyCtx.Set<OrderCorrectCase>().ToListAsync();
        Assert.Single(list);

        var forEachList = new List<OrderCorrectCase>();
        await verifyCtx.Set<OrderCorrectCase>().ForEachAsync(o => { forEachList.Add(o); return Task.CompletedTask; }, TimeSpan.FromSeconds(1));
        Assert.Single(forEachList);

        await verifyCtx.DisposeAsync();

        var options = new KsqlDslOptions
        {
            Common = new CommonSection { BootstrapServers = "localhost:9092" },
            SchemaRegistry = new SchemaRegistrySection { Url = "http://localhost:8088" }
        };

        await using var ctx = new WrongCaseContext(options);

        var set = ctx.Set<OrderWrongCase>();

        await Assert.ThrowsAsync<SchemaRegistryException>(() =>
            set.AddAsync(new OrderWrongCase
            {
                CustomerId = 1,
                Id = 1,
                region = "west",
                Amount = 5d
            }));

        await ctx.DisposeAsync();
    }
}
