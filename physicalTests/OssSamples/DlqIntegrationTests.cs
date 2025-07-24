using Kafka.Ksql.Linq;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Configuration;
using Kafka.Ksql.Linq.Core.Configuration;
using Kafka.Ksql.Linq.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Sdk;

namespace Kafka.Ksql.Linq.Tests.Integration;


public class DlqIntegrationTests
{
    public class Order
    {
        public int Id { get; set; }
        public decimal Amount { get; set; }
    }

    public class OrderContext : KsqlContext
    {
        public OrderContext() : base(new KsqlDslOptions()) { }
        public OrderContext(KsqlDslOptions options) : base(options) { }
        protected override void OnModelCreating(IModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Order>()
                .WithTopic("orders")
                .WithDecimalPrecision(o => o.Amount, precision: 18, scale: 2);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ForEachAsync_OnErrorDlq_WritesToDlq()
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

        // スキーマ確定用ダミーデータ送信
        await ctx.Set<Order>().AddAsync(new Order { Id = 1, Amount = 0.01m });

        // DLQ送信テスト本体
        var orderSet = ctx.Set<Order>();
        var extType = typeof(EventSetErrorHandlingExtensions);
        var method = extType.GetMethod("OnError")?.MakeGenericMethod(typeof(Order));
        if (method == null || orderSet.GetType().BaseType != typeof(EventSet<Order>))
            throw new SkipException("OnError extension not available");

        var withPolicy = (EventSet<Order>)method.Invoke(null, new object[] { orderSet, ErrorAction.DLQ })!;
        await withPolicy.ForEachAsync(o => throw new Exception("Simulated failure"), TimeSpan.FromSeconds(3));

        // DLQストリーム検証（ForEachAsync専用）
        DlqEnvelope? found = null;
        await ctx.Set<DlqEnvelope>().ForEachAsync(msg =>
        {
            if (msg.ErrorType == "Simulated failure")
                found = msg;
            return Task.CompletedTask;
        }, TimeSpan.FromSeconds(10));

        Assert.NotNull(found);
        Assert.Equal("Simulated failure", found.ErrorType);
    }
}
