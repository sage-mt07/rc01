using Kafka.Ksql.Linq.Application;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Core.Context;
using Kafka.Ksql.Linq.Core.Modeling;
using Kafka.Ksql.Linq.Mapping;
using Kafka.Ksql.Linq.Messaging.Abstractions;
using Kafka.Ksql.Linq.Messaging.Producers.Core;
using Kafka.Ksql.Linq.Configuration;
using Kafka.Ksql.Linq.Query.Analysis;
using System.Linq;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Mapping;

public class FullAutoQueryFlowTests
{
    private record User(int Id, string Name);

    private class StubProducer<T> : IKafkaProducer<T> where T : class
    {
        public bool Sent;
        public string TopicName => "users";
        public Task<KafkaDeliveryResult> SendAsync(T message, KafkaMessageContext? context = null, CancellationToken cancellationToken = default)
        {
            Sent = true;
            return Task.FromResult(new KafkaDeliveryResult());
        }
        public Task<KafkaBatchDeliveryResult> SendBatchAsync(IEnumerable<T> messages, KafkaMessageContext? context = null, CancellationToken cancellationToken = default)
        {
            Sent = true;
            return Task.FromResult(new KafkaBatchDeliveryResult());
        }
        public Task FlushAsync(System.TimeSpan timeout) => Task.CompletedTask;
        public void Dispose() { }
    }

    private class TestContext : KsqlContext
    {
        public TestContext() : base() { }
        protected override bool SkipSchemaRegistration => true;
        public void SetProducerManager(KafkaProducerManager manager)
        {
            typeof(KsqlContext).GetField("_producerManager", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(this, manager);
        }
        protected override void OnModelCreating(IModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>()
                .WithTopic("users")
                .HasKey(u => u.Id);
        }
    }

    [Fact]
    public async Task EntitySet_Query_To_AddAsync_FullFlow()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IMappingManager, MappingManager>();
        services.AddSingleton<TestContext>();
        var provider = services.BuildServiceProvider();
        var ctx = provider.GetRequiredService<TestContext>();

        var manager = new KafkaProducerManager(Options.Create(new KsqlDslOptions()), null);
        ctx.SetProducerManager(manager);
        var stub = new StubProducer<User>();
        var dict = (ConcurrentDictionary<System.Type, object>)typeof(KafkaProducerManager)
            .GetField("_producers", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(manager)!;
        dict[typeof(User)] = stub;

        var mapping = provider.GetRequiredService<IMappingManager>();
        mapping.Register<User>(ctx.GetEntityModels()[typeof(User)]);

        var result = QueryAnalyzer.AnalyzeQuery<User, User>(
            src => src.Where(u => u.Id == 1));
        Assert.True(result.Success);

        var user = new User(1, "Alice");
        var (key, value) = mapping.ExtractKeyValue(user);
        Assert.Equal(user.Id, key);
        Assert.Same(user, value);

        await ctx.Set<User>().AddAsync(user);
        Assert.True(stub.Sent);
    }

    [Fact]
    public async Task QuerySchemaHelper_Validate_Summary_FullFlow()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IMappingManager, MappingManager>();
        services.AddSingleton<TestContext>();
        var provider = services.BuildServiceProvider();
        var ctx = provider.GetRequiredService<TestContext>();

        var manager = new KafkaProducerManager(Options.Create(new KsqlDslOptions()), null);
        ctx.SetProducerManager(manager);
        var stub = new StubProducer<User>();
        var dict = (ConcurrentDictionary<System.Type, object>)typeof(KafkaProducerManager)
            .GetField("_producers", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(manager)!;
        dict[typeof(User)] = stub;

        var mapping = provider.GetRequiredService<IMappingManager>();
        mapping.Register<User>(ctx.GetEntityModels()[typeof(User)]);

        var result = QueryAnalyzer.AnalyzeQuery<User, User>(
            src => src.Where(u => u.Id == 1));
        Assert.True(result.Success);

        var schema = result.Schema!;
        Assert.True(QuerySchemaHelper.ValidateQuerySchema(schema, out var errors));
        Assert.Empty(errors);

        var summary = QuerySchemaHelper.GetSchemaSummary(schema);
        Assert.Contains("user", summary);

        var user = new User(1, "Alice");
        await ctx.Set<User>().AddAsync(user);
        Assert.True(stub.Sent);
    }
}
