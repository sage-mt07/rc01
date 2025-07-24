using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Application;
using Kafka.Ksql.Linq;
using Kafka.Ksql.Linq.Configuration;
using Kafka.Ksql.Linq.Core.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Sdk;

namespace Kafka.Ksql.Linq.Tests.Integration;


public class DummyFlagSchemaRecognitionTests
{

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
            modelBuilder.Entity<OrderValue>().WithTopic("orders");
            modelBuilder.Entity<Customer>().WithTopic("customers");
            modelBuilder.Entity<EventLog>().WithTopic("events");
            modelBuilder.Entity<NullableOrder>().WithTopic("orders_nullable");
            modelBuilder.Entity<NullableKeyOrder>().WithTopic("orders_nullable_key");
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

        var headers = new Dictionary<string, string> { ["is_dummy"] = "true" };

        await ctx.Set<OrderValue>().AddAsync(new OrderValue
        {
            CustomerId = 1,
            Id = 1,
            Region = "east",
            Amount = 10d,
            IsHighPriority = false,
            Count = 1
        }, headers);
        await ctx.Set<Customer>().AddAsync(new Customer { Id = 1, Name = "alice" }, headers);
        await ctx.Set<EventLog>().AddAsync(new EventLog { Level = 1, Message = "init" }, headers);
        await ctx.Set<NullableOrder>().AddAsync(new NullableOrder { CustomerId = 1, Region = "east", Amount = 10d }, headers);
        await ctx.Set<NullableKeyOrder>().AddAsync(new NullableKeyOrder { CustomerId = 1, Amount = 10d }, headers);

        var timeout = TimeSpan.FromSeconds(5);
        await ctx.WaitForEntityReadyAsync<OrderValue>(timeout);
        await ctx.WaitForEntityReadyAsync<Customer>(timeout);
        await ctx.WaitForEntityReadyAsync<EventLog>(timeout);
        await ctx.WaitForEntityReadyAsync<NullableOrder>(timeout);
        await ctx.WaitForEntityReadyAsync<NullableKeyOrder>(timeout);

        await ctx.DisposeAsync();
    }

    // ダミーメッセージを送信しスキーマを登録後、各クエリが実行可能か確認
    [Fact]
    [Trait("Category", "Integration")]
    public async Task DummyMessages_EnableQueries()
    {
        if (!KsqlDbAvailability.IsAvailable())
            throw new SkipException(KsqlDbAvailability.SkipReason);
        try
        {
            await TestEnvironment.ResetAsync();

        }
        catch (Exception)
        {
        }

        await using (var ctx = TestEnvironment.CreateContext())
        {
            foreach (var ddl in TestSchema.GenerateTableDdls())
            {
                await ctx.ExecuteStatementAsync(ddl);
            }
        }

        await ProduceDummyRecordsAsync();

        var queries = new[]
        {
            "SELECT * FROM ORDERS EMIT CHANGES LIMIT 1;",
            "SELECT * FROM CUSTOMERS EMIT CHANGES LIMIT 1;",
            "SELECT COUNT(*) FROM EVENTS;",
            "SELECT REGION, COUNT(*) FROM ORDERS GROUP BY REGION EMIT CHANGES LIMIT 1;"
        };

        await using (var ctx = TestEnvironment.CreateContext())
        {
            foreach (var q in queries)
            {
                var r = await ctx.ExecuteExplainAsync(q);
                Assert.True(r.IsSuccess, $"{q} failed: {r.Message}");
            }
        }
    }

}
