using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection;
using Kafka.Ksql.Linq;
using Kafka.Ksql.Linq.Core.Context;
using Kafka.Ksql.Linq.Configuration;
using Kafka.Ksql.Linq.Messaging.Configuration;
using Kafka.Ksql.Linq.Core.Configuration;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Application;

public class KsqlDbExecutionExtensionsTests
{
    private class DummyContext : KsqlContext
    {
        public DummyContext(KafkaContextOptions opt) : base(opt) { }
        protected override bool SkipSchemaRegistration => true;
        protected override void OnModelCreating(Kafka.Ksql.Linq.Core.Abstractions.IModelBuilder modelBuilder) { }
    }

    [Fact]
    public void CreateClient_UsesSchemaRegistryHost()
    {
        var ctx = new DummyContext(new KafkaContextOptions());
        var field = typeof(KsqlContext).GetField("_dslOptions", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var opts = (KsqlDslOptions)field.GetValue(ctx)!;
        var schema = opts.SchemaRegistry;
        typeof(SchemaRegistrySection).GetProperty("Url")!.SetValue(schema, "http://example.com:8085");

        var method = typeof(KsqlContext)
            .GetMethod("CreateClient", BindingFlags.NonPublic | BindingFlags.Static)!;
        using var client = (HttpClient)method.Invoke(null, new object[] { ctx })!;
        Assert.Equal(new Uri("http://example.com:8085"), client.BaseAddress);
    }

    [Fact]
    public void CreateClient_FallsBackToBootstrapServers()
    {
        var ctx = new DummyContext(new KafkaContextOptions());
        var field = typeof(KsqlContext).GetField("_dslOptions", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var opts = (KsqlDslOptions)field.GetValue(ctx)!;
        typeof(SchemaRegistrySection).GetProperty("Url")!.SetValue(opts.SchemaRegistry, "");
        typeof(CommonSection).GetProperty("BootstrapServers")!.SetValue(opts.Common, "example.com:9092");

        var method = typeof(KsqlContext)
            .GetMethod("CreateClient", BindingFlags.NonPublic | BindingFlags.Static)!;
        using var client = (HttpClient)method.Invoke(null, new object[] { ctx })!;
        Assert.Equal(new Uri("http://example.com:9092"), client.BaseAddress);
    }
}
