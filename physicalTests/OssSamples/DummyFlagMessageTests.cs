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

        await Assert.ThrowsAsync<InvalidOperationException>(() => ctx.Set<OrderValue>().ToListAsync());

        var consumed = new List<OrderValue>();
        await ctx.Set<OrderValue>().ForEachAsync(o => { consumed.Add(o); return Task.CompletedTask; }, TimeSpan.FromSeconds(1));
        Assert.Single(consumed);

        await ctx.DisposeAsync();
    }
}
