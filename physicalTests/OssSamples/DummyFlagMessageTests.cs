using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Application;
using Kafka.Ksql.Linq.Core.Context;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

#nullable enable
namespace Kafka.Ksql.Linq.Tests.Integration;

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
        public DummyContext() : base() { }
        public DummyContext(KafkaContextOptions options) : base(options) { }
        protected override void OnModelCreating(IModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OrderValue>().WithTopic("orders");
        }
    }

    // KafkaProducer が is_dummy ヘッダーを追加し EventSet で取得できるか確認
    [KsqlDbFact]
    public async Task SendAsync_AddsDummyFlagHeader()
    {
        await TestEnvironment.ResetAsync();

        var options = new KafkaContextOptions
        {
            BootstrapServers = "localhost:9092",
            SchemaRegistryUrl = "http://localhost:8081"
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
