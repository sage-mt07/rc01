using System;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Confluent.SchemaRegistry;
using static Kafka.Ksql.Linq.Tests.PrivateAccessor;
using Microsoft.Extensions.Logging.Abstractions;
using Kafka.Ksql.Linq.Configuration.Options;
using Kafka.Ksql.Linq.Serialization.Avro;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Serialization;

public class ResilientAvroSerializerManagerTests
{

    private static ResilientAvroSerializerManager CreateUninitialized()
    {
        var mgr = (ResilientAvroSerializerManager)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(ResilientAvroSerializerManager));
        typeof(ResilientAvroSerializerManager).GetField("_logger", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(mgr, NullLogger<ResilientAvroSerializerManager>.Instance);
        return mgr;
    }

    [Theory]
    [InlineData("topic-key", "topic")]
    [InlineData("topic-value", "topic")]
    [InlineData("name", "name")]
    public void ExtractTopicFromSubject_ReturnsTopic(string subject, string expected)
    {
        var mgr = CreateUninitialized();
        var result = InvokePrivate<string>(mgr, "ExtractTopicFromSubject", new[] { typeof(string) }, null, subject);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ShouldRetry_RespectsPolicy()
    {
        var mgr = CreateUninitialized();
        var policy = new AvroRetryPolicy { MaxAttempts = 3, RetryableExceptions = { typeof(TimeoutException) } };
        var ex = new TimeoutException();
        var result = InvokePrivate<bool>(mgr, "ShouldRetry", new[] { typeof(Exception), typeof(AvroRetryPolicy), typeof(int) }, null, ex, policy, 1);
        Assert.True(result);
        Assert.False(InvokePrivate<bool>(mgr, "ShouldRetry", new[] { typeof(Exception), typeof(AvroRetryPolicy), typeof(int) }, null, ex, policy, 3));
    }

    [Fact]
    public void CalculateDelay_AppliesBackoff()
    {
        var mgr = CreateUninitialized();
        var policy = new AvroRetryPolicy
        {
            InitialDelay = TimeSpan.FromMilliseconds(100),
            BackoffMultiplier = 2,
            MaxDelay = TimeSpan.FromMilliseconds(500)
        };
        var delay1 = InvokePrivate<TimeSpan>(mgr, "CalculateDelay", new[] { typeof(AvroRetryPolicy), typeof(int) }, null, policy, 1);
        var delay2 = InvokePrivate<TimeSpan>(mgr, "CalculateDelay", new[] { typeof(AvroRetryPolicy), typeof(int) }, null, policy, 3);
        Assert.Equal(TimeSpan.FromMilliseconds(100), delay1);
        Assert.Equal(TimeSpan.FromMilliseconds(400), delay2);
}

    private static ResilientAvroSerializerManager CreateManager(FakeSchemaRegistryClient fake)
    {
        var options = new AvroOperationRetrySettings
        {
            SchemaRegistration = new AvroRetryPolicy { MaxAttempts = 1 },
            SchemaRetrieval = new AvroRetryPolicy { MaxAttempts = 1 },
            CompatibilityCheck = new AvroRetryPolicy { MaxAttempts = 1 }
        };
        var proxy = DispatchProxy.Create<ISchemaRegistryClient, FakeSchemaRegistryClient>();
        var proxyFake = (FakeSchemaRegistryClient)proxy!;
        // copy settings from provided fake
        proxyFake.RegisterReturn = fake.RegisterReturn;
        proxyFake.CompatibilityResult = fake.CompatibilityResult;
        proxyFake.VersionsResult = fake.VersionsResult;
        proxyFake.SchemaString = fake.SchemaString;
        var mgr = new ResilientAvroSerializerManager(proxy, Microsoft.Extensions.Options.Options.Create(options), NullLogger<ResilientAvroSerializerManager>.Instance);
        return mgr;
    }

    [Fact]
    public async Task RegisterSchemaWithRetryAsync_ReturnsId()
    {
        var fake = new FakeSchemaRegistryClient { RegisterReturn = 42 };
        var mgr = CreateManager(fake);
        var id = await mgr.RegisterSchemaWithRetryAsync("s", "sc");
        Assert.Equal(42, id);
    }

    [Fact]
    public async Task GetSchemaWithRetryAsync_ReturnsInfo()
    {
        var fake = new FakeSchemaRegistryClient { RegisterReturn = 5, SchemaString = "abc" };
        var mgr = CreateManager(fake);
        var info = await mgr.GetSchemaWithRetryAsync("topic-value", 1);
        Assert.Equal(5, info.ValueSchemaId);
        Assert.Equal("abc", info.ValueSchema);
    }

    [Fact]
    public async Task CheckCompatibilityWithRetryAsync_ReturnsValue()
    {
        var fake = new FakeSchemaRegistryClient { CompatibilityResult = false };
        var mgr = CreateManager(fake);
        var result = await mgr.CheckCompatibilityWithRetryAsync("s", "sc");
        Assert.False(result);
    }
}
