using Kafka.Ksql.Linq.Application;
using Kafka.Ksql.Linq;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Configuration;
using Kafka.Ksql.Linq.Core.Configuration;
using Kafka.Ksql.Linq.Entities.Samples.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Sdk;

namespace Kafka.Ksql.Linq.Tests.Integration;


public class CompositeKeyPocoTests
{
    public class OrderContext : KsqlContext
    {
        public OrderContext() : base(new KsqlDslOptions()) { }
        public OrderContext(KsqlDslOptions options) : base(options) { }
        protected override void OnModelCreating(IModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Order>()
                .WithTopic("orders_multi_pk")
                .HasKey(o => new { o.OrderId, o.UserId });
        }
    }

    [Fact(Skip = "Composite primary key DDL is not supported in ksqlDB.")]
    [Trait("Category", "Integration")]
    public async Task SendAndReceive_CompositeKeyPoco()
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

        await ctx.Set<Order>().AddAsync(new Order
        {
            OrderId = 1,
            UserId = 2,
            ProductId = 3,
            Quantity = 4
        });

        var list = await ctx.Set<Order>().ToListAsync();
        Assert.Single(list);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ctx.Set<Order>().ForEachAsync(o => { return Task.CompletedTask; }, TimeSpan.FromSeconds(1)));

        await ctx.DisposeAsync();
    }
}
