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

        await Assert.ThrowsAsync<InvalidOperationException>(() => ctx.Set<OrderCorrectCase>().ToListAsync());


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
