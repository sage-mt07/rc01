using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection;
using Kafka.Ksql.Linq.Application;
using Kafka.Ksql.Linq.Core.Context;
using Microsoft.Extensions.Configuration;
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
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                { "KsqlDsl:SchemaRegistry:Url", "http://example.com:8085" }
            })
            .Build();

        var options = new KafkaContextOptions { Configuration = config };
        var ctx = new DummyContext(options);

        var method = typeof(KsqlDbExecutionExtensions)
            .GetMethod("CreateClient", BindingFlags.NonPublic | BindingFlags.Static)!;
        using var client = (HttpClient)method.Invoke(null, new object[] { ctx })!;
        Assert.Equal(new Uri("http://example.com:8085"), client.BaseAddress);
    }

    [Fact]
    public void CreateClient_FallsBackToBootstrapServers()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                { "KsqlDsl:Common:BootstrapServers", "example.com:9092" }
            })
            .Build();

        var options = new KafkaContextOptions { Configuration = config };
        var ctx = new DummyContext(options);

        var method = typeof(KsqlDbExecutionExtensions)
            .GetMethod("CreateClient", BindingFlags.NonPublic | BindingFlags.Static)!;
        using var client = (HttpClient)method.Invoke(null, new object[] { ctx })!;
        Assert.Equal(new Uri("http://example.com:9092"), client.BaseAddress);
    }
}
