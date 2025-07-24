using Confluent.Kafka;
using Kafka.Ksql.Linq;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Configuration;
using Kafka.Ksql.Linq.Core.Configuration;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Sdk;

namespace Kafka.Ksql.Linq.Tests.Integration;


public class AdvancedDataTypeTests
{
    public enum Status { Pending, Done }

    public class Record
    {
        public int Id { get; set; }
        public decimal Price { get; set; }
        public DateTime Created { get; set; }
        public Status State { get; set; }
    }

    public class RecordContext : KsqlContext
    {
        public RecordContext() : base(new KsqlDslOptions()) { }
        public RecordContext(KsqlDslOptions options) : base(options) { }
        protected override void OnModelCreating(IModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Record>()
                .WithTopic("records")
                .WithDecimalPrecision(r => r.Price, precision: 18, scale: 4);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Decimal_DateTime_Enum_RoundTrip()
    {
        if (!KsqlDbAvailability.IsAvailable())
            throw new SkipException(KsqlDbAvailability.SkipReason);

        try
        {
            await TestEnvironment.ResetAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Warning] ResetAsync failed: {ex}");
            throw new SkipException($"Test setup failed in ResetAsync: {ex.Message}");
        }

        var options = new KsqlDslOptions
        {
            Common = new CommonSection { BootstrapServers = TestEnvironment.KafkaBootstrapServers },
            SchemaRegistry = new SchemaRegistrySection { Url = TestEnvironment.SchemaRegistryUrl }
        };

        await using var ctx = new RecordContext(options);

        var data = new Record { Id = 1, Price = 12.3456m, Created = DateTime.UtcNow, State = Status.Done };
        await ctx.Set<Record>().AddAsync(data);

        var list = new List<Record>();
        await ctx.Set<Record>().ForEachAsync(r => { list.Add(r); return Task.CompletedTask; }, TimeSpan.FromSeconds(1));
        Assert.Single(list);
        Assert.Equal(data.Price, list[0].Price);
        Assert.Equal(data.State, list[0].State);
        Assert.True(Math.Abs((list[0].Created - data.Created).TotalMinutes) < 1);
    }
}
