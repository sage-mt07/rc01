using Kafka.Ksql.Linq;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Application;
using Kafka.Ksql.Linq.Configuration;
using Kafka.Ksql.Linq.Core.Configuration;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Sdk;

namespace Kafka.Ksql.Linq.Tests.Integration;

public class JoinIntegrationTests
{
    public class OrderValue
    {
        public int CustomerId { get; set; }
        public int Id { get; set; }
        public string Region { get; set; } = string.Empty;
        public double Amount { get; set; }
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

    public class JoinContext : KsqlContext
    {
        public JoinContext() : base(new KsqlDslOptions()) { }
        public JoinContext(KsqlDslOptions options) : base(options) { }
        protected override void OnModelCreating(IModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OrderValue>().WithTopic("orders");
            modelBuilder.Entity<Customer>().WithTopic("customers");
            modelBuilder.Entity<EventLog>().WithTopic("events");
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task TwoTableJoin_Query_ShouldBeValid()
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

        var options = new KsqlDslOptions
        {
            Common = new CommonSection { BootstrapServers = TestEnvironment.KafkaBootstrapServers },
            SchemaRegistry = new SchemaRegistrySection { Url = TestEnvironment.SchemaRegistryUrl }
        };

        await using var ctx = new JoinContext(options);

        var query = ctx.Entity<OrderValue>()
            .Join(ctx.Entity<Customer>(), o => o.CustomerId, c => c.Id, (o, c) => new { o, c })
            .Select(x => new { x.o.CustomerId, x.c.Name, x.o.Amount });

        var ksql = query.ToQueryString();
        var response = await ctx.ExecuteExplainAsync(ksql);
        Assert.True(response.IsSuccess, $"{ksql} failed: {response.Message}");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ThreeTableJoin_Query_ShouldBeValid()
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

        var options = new KsqlDslOptions
        {
            Common = new CommonSection { BootstrapServers = TestEnvironment.KafkaBootstrapServers },
            SchemaRegistry = new SchemaRegistrySection { Url = TestEnvironment.SchemaRegistryUrl }
        };

        await using var ctx = new JoinContext(options);

        var query = ctx.Entity<OrderValue>()
            .Join(ctx.Entity<Customer>(), o => o.CustomerId, c => c.Id, (o, c) => new { o, c })
            .Join(ctx.Entity<EventLog>(), oc => oc.o.Id, e => e.Level, (oc, e) => new { oc.o, oc.c, e })
            .Select(x => new { x.o.CustomerId, x.c.Name, x.e.Message });

        var ksql = query.ToQueryString();
        var response = await ctx.ExecuteExplainAsync(ksql);
        Assert.True(response.IsSuccess, $"{ksql} failed: {response.Message}");
    }
}
