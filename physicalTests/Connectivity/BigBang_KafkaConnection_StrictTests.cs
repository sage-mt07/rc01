using System;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using Kafka.Ksql.Linq;
using Kafka.Ksql.Linq.Application;
using Kafka.Ksql.Linq.Configuration;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Core.Configuration;
using Xunit;
using Xunit.Sdk;

namespace Kafka.Ksql.Linq.Tests.Integration;


public class BigBang_KafkaConnection_StrictTests
{
    public class Order
    {
        public int Id { get; set; }
        public double Amount { get; set; }
    }

    public class OrderContext : KsqlContext
    {
        public OrderContext() : base(new KsqlDslOptions()) { }
        public OrderContext(KsqlDslOptions options) : base(options) { }
        protected override void OnModelCreating(IModelBuilder modelBuilder)
            => modelBuilder.Entity<Order>().WithTopic("orders");
    }

    private static KsqlDslOptions CreateOptions() => new()
    {
        Common = new CommonSection { BootstrapServers = TestEnvironment.KafkaBootstrapServers },
        SchemaRegistry = new SchemaRegistrySection { Url = TestEnvironment.SchemaRegistryUrl }
    };

    [Fact]
    [Trait("Category", "Integration")]
    public async Task EX02_AddAsync_KafkaDown_ShouldLogAndTimeout()
    {
        if (!KsqlDbAvailability.IsAvailable())
            throw new SkipException(KsqlDbAvailability.SkipReason);

        await DockerHelper.StopServiceAsync("kafka");
        await using var ctx = new OrderContext(CreateOptions());
        var msg = new Order { Id = 1, Amount = 100 };

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var task = ctx.Set<Order>().AddAsync(msg, null, cts.Token);
        var completed = await Task.WhenAny(task, Task.Delay(11000));
        Assert.Same(task, completed);

        var ex = await Assert.ThrowsAsync<KafkaException>(() => task);
        Assert.Contains("connection refused", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task EX02_ForeachAsync_KafkaDown_ShouldLogAndTimeout()
    {
        if (!KsqlDbAvailability.IsAvailable())
            throw new SkipException(KsqlDbAvailability.SkipReason);

        await DockerHelper.StopServiceAsync("kafka");
        await using var ctx = new OrderContext(CreateOptions());

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var task = ctx.Set<Order>().ForEachAsync(_ => Task.CompletedTask, TimeSpan.FromSeconds(1), cts.Token);
        var completed = await Task.WhenAny(task, Task.Delay(11000));
        Assert.Same(task, completed);

        var ex = await Assert.ThrowsAsync<ConsumeException>(() => task);
        Assert.Contains("connection", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
