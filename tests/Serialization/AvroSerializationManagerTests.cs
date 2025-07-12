using System;
using System.Threading.Tasks;
using System.Reflection;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Serialization.Avro.Exceptions;
using Kafka.Ksql.Linq.Serialization.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Serialization;

public class AvroSerializationManagerTests
{
    private class Sample
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private static AvroSerializationManager<Sample> CreateManager(FakeSchemaRegistryClient fake)
    {
        var proxy = DispatchProxy.Create<Confluent.SchemaRegistry.ISchemaRegistryClient, FakeSchemaRegistryClient>();
        var proxyFake = (FakeSchemaRegistryClient)proxy!;
        proxyFake.RegisterReturn = fake.RegisterReturn;
        proxyFake.CompatibilityResult = fake.CompatibilityResult;
        proxyFake.LatestVersion = fake.LatestVersion;
        proxyFake.SchemaString = fake.SchemaString;
        var mgr = new AvroSerializationManager<Sample>(proxy, new NullLoggerFactory());
        return mgr;
    }

    [Fact]
    public async Task GetSerializers_And_Deserializers_Work()
    {
        var mgr = CreateManager(new FakeSchemaRegistryClient());
        var ser1 = await mgr.GetSerializersAsync();
        var ser2 = await mgr.GetSerializersAsync();
        Assert.Same(ser1, ser2); // cached
        var des1 = await mgr.GetDeserializersAsync();
        var des2 = await mgr.GetDeserializersAsync();
        Assert.Same(des1, des2);
        Assert.Equal(typeof(Sample), mgr.EntityType);
    }

    [Fact]
    public async Task ValidateRoundTripAsync_ReturnsTrue()
    {
        var mgr = CreateManager(new FakeSchemaRegistryClient());
        var result = await mgr.ValidateRoundTripAsync(new Sample { Id = 1, Name = "a" });
        Assert.True(result);
    }

    [Fact]
    public async Task CanUpgradeSchemaAsync_ReturnsValue()
    {
        var fake = new FakeSchemaRegistryClient { CompatibilityResult = false };
        var mgr = CreateManager(fake);
        var result = await mgr.CanUpgradeSchemaAsync();
        Assert.False(result);
    }

    [Fact]
    public async Task UpgradeSchemaAsync_Incompatible_ReturnsFailure()
    {
        var fake = new FakeSchemaRegistryClient { CompatibilityResult = false };
        var mgr = CreateManager(fake);
        var result = await mgr.UpgradeSchemaAsync();
        Assert.False(result.Success);
        Assert.Equal(SchemaRegistrationFailureCategory.SchemaIncompatible, result.FailureCategory);
        Assert.Contains("compatible", result.Reason);
    }

    [Fact]
    public async Task UpgradeSchemaAsync_Success_ClearsCache()
    {
        var fake = new FakeSchemaRegistryClient { RegisterReturn = 10 };
        var mgr = CreateManager(fake);
        var first = await mgr.GetSerializersAsync();
        var res = await mgr.UpgradeSchemaAsync();
        Assert.True(res.Success);
        var second = await mgr.GetSerializersAsync();
        Assert.NotSame(first, second); // cache cleared
    }

    [Fact]
    public async Task GetCurrentSchemasAsync_ReturnsSchemas()
    {
        var mgr = CreateManager(new FakeSchemaRegistryClient());
        var schemas = await mgr.GetCurrentSchemasAsync();
        Assert.Contains("record", schemas.valueSchema);
    }

    [Fact]
    public async Task ClearCache_RemovesManager()
    {
        var mgr = CreateManager(new FakeSchemaRegistryClient());
        var first = await mgr.GetSerializersAsync();
        mgr.ClearCache();
        var second = await mgr.GetSerializersAsync();
        Assert.NotSame(first, second);
    }

    [Fact]
    public async Task Dispose_ClearsCache()
    {
        var mgr = CreateManager(new FakeSchemaRegistryClient());
        var first = await mgr.GetSerializersAsync();
        mgr.Dispose();
        var second = await mgr.GetSerializersAsync();
        Assert.NotSame(first, second);
    }

    [Fact]
    public async Task GetStatistics_ReflectsOperations()
    {
        var mgr = CreateManager(new FakeSchemaRegistryClient());
        await mgr.GetSerializersAsync();
        await mgr.GetDeserializersAsync();
        var stats = mgr.GetStatistics();
        Assert.True(stats.TotalSerializations > 0 || stats.TotalDeserializations > 0);
    }
}
