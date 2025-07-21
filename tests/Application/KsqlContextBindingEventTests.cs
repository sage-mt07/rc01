using Kafka.Ksql.Linq.Application;
using Kafka.Ksql.Linq.Configuration;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Core.Context;
using Kafka.Ksql.Linq.Cache.Core;
using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;
#nullable enable

namespace Kafka.Ksql.Linq.Tests.Application;

public class KsqlContextBindingEventTests
{
    private class BindingContext : KsqlContext
    {
        public BindingContext() : base(new KafkaContextOptions()) { }

        protected override bool SkipSchemaRegistration => true;

        protected override void OnModelCreating(IModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Sample>();
        }
    }

    private class Sample
    {
        public int Id { get; set; }
    }

    [Fact]
    public void TableCacheRegistry_IsInitialized()
    {
        var ctx = new BindingContext();

        var optionsField = typeof(KsqlContext).GetField("_dslOptions", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var options = (KsqlDslOptions)optionsField.GetValue(ctx)!;
        options.Entities.Add(new EntityConfiguration { Entity = nameof(Sample), SourceTopic = "s" });

        Kafka.Ksql.Linq.Cache.Extensions.KsqlContextCacheExtensions.UseTableCache(ctx, options);

        var registryField = typeof(KsqlContext).GetField("_cacheRegistry", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var registry = registryField.GetValue(ctx);
        Assert.NotNull(registry);
    }
}
