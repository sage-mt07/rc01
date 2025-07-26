using Kafka.Ksql.Linq;
using Kafka.Ksql.Linq.Application;
using Kafka.Ksql.Linq.Configuration;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Core.Configuration;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Xunit.Sdk;

namespace Kafka.Ksql.Linq.Tests.Integration;

public class NoKeyPocoTests
{
    public class Record
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class RecordContext : KsqlContext
    {
        public RecordContext() : base(new KsqlDslOptions()) { }
        public RecordContext(KsqlDslOptions options) : base(options) { }
        protected override void OnModelCreating(IModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Record>().WithTopic("records_no_key");
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SendAndReceive_NoKeyRecord()
    {
        if (!KsqlDbAvailability.IsAvailable())
            throw new SkipException(KsqlDbAvailability.SkipReason);

        await TestEnvironment.ResetAsync();

        var options = new KsqlDslOptions
        {
            Common = new CommonSection { BootstrapServers = TestEnvironment.KafkaBootstrapServers },
            SchemaRegistry = new SchemaRegistrySection { Url = TestEnvironment.SchemaRegistryUrl }
        };

        await using var ctx = new RecordContext(options);

        var data = new Record { Id = 1, Name = "alice" };
        await ctx.Set<Record>().AddAsync(data);

        var list = new List<Record>();
        await ctx.Set<Record>().ForEachAsync(r => { list.Add(r); return Task.CompletedTask; }, TimeSpan.FromSeconds(1));

        Assert.Single(list);
        Assert.Equal(data.Id, list[0].Id);
        Assert.Equal(data.Name, list[0].Name);

        await ctx.DisposeAsync();
    }
}
