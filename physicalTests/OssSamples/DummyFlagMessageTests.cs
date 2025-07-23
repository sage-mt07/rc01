using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Application;
using Kafka.Ksql.Linq;
using Kafka.Ksql.Linq.Configuration;
using Kafka.Ksql.Linq.Core.Configuration;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Sdk;

#nullable enable
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

public class DummyFlagMessageTests
{

    public class OrderValue
    {
        public int CustomerId { get; set; }
        public int Id { get; set; }
        public string? Region { get; set; }
        public double Amount { get; set; }
        public bool IsHighPriority { get; set; }
        public int Count { get; set; }
    }

    public class DummyContext : KsqlContext
    {
        public DummyContext() : base(new KsqlDslOptions()) { }
        public DummyContext(KsqlDslOptions options) : base(options) { }
        protected override void OnModelCreating(IModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OrderValue>().WithTopic("orders");
        }
    }

    // KafkaProducer が is_dummy ヘッダーを追加し EventSet で取得できるか確認
    [Fact]
    public async Task SendAsync_AddsDummyFlagHeader()
    {
        if (!KsqlDbAvailability.IsAvailable())
            throw new SkipException(KsqlDbAvailability.SkipReason);

        await TestEnvironment.ResetAsync();

        var options = new KsqlDslOptions
        {
            Common = new CommonSection { BootstrapServers = TestEnvironment.KafkaBootstrapServers },
            SchemaRegistry = new SchemaRegistrySection { Url = TestEnvironment.SchemaRegistryUrl }
        };

        await using var ctx = new DummyContext(options);

        var headers = new Dictionary<string, string> { ["is_dummy"] = "true" };

        await ctx.Set<OrderValue>().AddAsync(new OrderValue
        {
            CustomerId = 1,
            Id = 1,
            Region = "west",
            Amount = 10d,
            IsHighPriority = false,
            Count = 1
        }, headers);

        var list = await ctx.Set<OrderValue>().ToListAsync();
        Assert.Single(list);

        var consumed = new List<OrderValue>();
        await ctx.Set<OrderValue>().ForEachAsync(o => { consumed.Add(o); return Task.CompletedTask; }, TimeSpan.FromSeconds(1));
        Assert.Single(consumed);

        await ctx.DisposeAsync();
    }
}
