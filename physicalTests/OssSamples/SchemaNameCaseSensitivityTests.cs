using Confluent.SchemaRegistry;
using Kafka.Ksql.Linq.Application;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq;
using Kafka.Ksql.Linq.Configuration;
using Kafka.Ksql.Linq.Core.Configuration;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Sdk;

namespace Kafka.Ksql.Linq.Tests.Integration;

internal static class KsqlDbAvailability
{
    public const string SkipReason = "Skipped in CI due to missing ksqlDB instance or schema setup failure";
    private static bool _available;
    private static DateTime? _lastFailure;
    private static readonly object _sync = new();

    public static bool IsAvailable()
    {
        lock (_sync)
        {
            if (_available)
                return true;

            if (_lastFailure.HasValue && DateTime.UtcNow - _lastFailure.Value < TimeSpan.FromSeconds(5))
                return false;

            const int attempts = 3;
            for (var i = 0; i < attempts; i++)
            {
                try
                {
                    using var ctx = TestEnvironment.CreateContext();
                    var r = ctx.ExecuteStatementAsync("SHOW TOPICS;").GetAwaiter().GetResult();
                    if (r.IsSuccess)
                    {
                        _available = true;
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ksqlDB check attempt {i + 1} failed: {ex.Message}");
                }

                Thread.Sleep(1000);
            }

            _available = false;
            _lastFailure = DateTime.UtcNow;
            return false;
        }
    }
}

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

    // スキーマ定義と異なるフィールド名の大文字小文字違いを送信した場合に例外が発生するか確認
    [Fact]
    [Trait("Category", "Integration")]
    public async Task MismatchedFieldCase_ShouldThrowException()
    {
        if (!KsqlDbAvailability.IsAvailable())
            throw new SkipException(KsqlDbAvailability.SkipReason);

        await TestEnvironment.ResetAsync();

        var options = new KsqlDslOptions
        {
            Common = new CommonSection { BootstrapServers = TestEnvironment.KafkaBootstrapServers },
            SchemaRegistry = new SchemaRegistrySection { Url = TestEnvironment.SchemaRegistryUrl }
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

        var list = await ctx.Set<OrderCorrectCase>().ToListAsync();
        Assert.Single(list);

        var forEachList = new List<OrderCorrectCase>();
        await ctx.Set<OrderCorrectCase>().ForEachAsync(o => { forEachList.Add(o); return Task.CompletedTask; }, TimeSpan.FromSeconds(1));
        Assert.Single(forEachList);

        await using var wrongCtx = new WrongCaseContext(options);

        await Assert.ThrowsAsync<SchemaRegistryException>(() =>
            wrongCtx.Set<OrderWrongCase>().AddAsync(new OrderWrongCase
            {
                CustomerId = 1,
                Id = 1,
                region = "west",
                Amount = 5d
            }, headers));
    }
}
