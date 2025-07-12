using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Confluent.Kafka;
using Confluent.SchemaRegistry;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Serialization.Avro.Cache;
using Kafka.Ksql.Linq.Serialization.Avro.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static Kafka.Ksql.Linq.Tests.PrivateAccessor;

namespace Kafka.Ksql.Linq.Tests.Serialization;

public class AvroSerializerCacheTests
{
    private class SampleEntity
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    private static AvroSerializerCache CreateCache()
    {
        var client = DispatchProxy.Create<ISchemaRegistryClient, FakeSchemaRegistryClient>();
        var factory = new AvroSerializerFactory(client, new NullLoggerFactory());
        return new AvroSerializerCache(factory, new NullLoggerFactory());
    }

    [Fact]
    public void GetAvroManager_CachesInstance()
    {
        using var cache = CreateCache();
        var m1 = cache.GetAvroManager<SampleEntity>();
        var m2 = cache.GetAvroManager<SampleEntity>();
        Assert.Same(m1, m2);
    }

    [Fact]
    public void ClearCache_RemovesManager()
    {
        using var cache = CreateCache();
        var m1 = cache.GetAvroManager<SampleEntity>();
        cache.ClearCache<SampleEntity>();
        var m2 = cache.GetAvroManager<SampleEntity>();
        Assert.NotSame(m1, m2);
    }

    [Fact]
    public void Dispose_ClearsManagers()
    {
        var cache = CreateCache();
        var m1 = cache.GetAvroManager<SampleEntity>();
        cache.Dispose();
        var m2 = cache.GetAvroManager<SampleEntity>();
        Assert.NotSame(m1, m2);
    }

    [Fact]
    public void GenerateCacheKey_UsesTypeName()
    {
        using var cache = CreateCache();
        var mgr = cache.GetAvroManager<SampleEntity>();
        var key = InvokePrivate<string>(mgr, "GenerateCacheKey", Type.EmptyTypes);
        Assert.Contains(typeof(SampleEntity).FullName!, key);
    }

    [Fact]
    public void EntityType_ReturnsCorrectType()
    {
        using var cache = CreateCache();
        var mgr = cache.GetAvroManager<SampleEntity>();
        Assert.Equal(typeof(SampleEntity), mgr.EntityType);
    }

    [Fact]
    public void GetEntityModel_ReturnsKeyInfo()
    {
        using var cache = CreateCache();
        var mgr = cache.GetAvroManager<SampleEntity>();
        var model = InvokePrivate<EntityModel>(mgr, "GetEntityModel", Type.EmptyTypes, new[] { typeof(SampleEntity) });
        Assert.Equal(0, model.KeyProperties.Length);
        Assert.Equal(typeof(SampleEntity), model.EntityType);
    }

    [Fact]
    public async Task GetSerializersAsync_CachesResults()
    {
        using var cache = CreateCache();
        var mgr = cache.GetAvroManager<SampleEntity>();
        var first = await mgr.GetSerializersAsync();
        var statsAfterFirst = mgr.GetStatistics();
        var second = await mgr.GetSerializersAsync();
        Assert.Same(first, second);
        Assert.Equal(1, statsAfterFirst.CacheMisses);
        Assert.Equal(1, mgr.GetStatistics().CacheHits);
    }

    [Fact]
    public async Task GetDeserializersAsync_CachesResults()
    {
        using var cache = CreateCache();
        var mgr = cache.GetAvroManager<SampleEntity>();
        var first = await mgr.GetDeserializersAsync();
        var second = await mgr.GetDeserializersAsync();
        Assert.Same(first, second);
        Assert.Equal(1, mgr.GetStatistics().CacheHits);
    }

    [Fact]
    public async Task ValidateRoundTripAsync_ReturnsTrue()
    {
        using var cache = CreateCache();
        var mgr = cache.GetAvroManager<SampleEntity>();
        var entity = new SampleEntity { Id = 1, Code = "c", Name = "n" };
        var result = await mgr.ValidateRoundTripAsync(entity);
        Assert.True(result);
    }

    [Fact]
    public async Task ManagerDispose_ClearsCaches()
    {
        var cache = CreateCache();
        var mgr = cache.GetAvroManager<SampleEntity>();
        var first = await mgr.GetSerializersAsync();
        mgr.Dispose();
        var second = await mgr.GetSerializersAsync();
        Assert.NotSame(first, second);
    }

    [Fact]
    public void AvroCompositeKeySerializer_RoundTrip()
    {
        var proxy = DispatchProxy.Create<ISchemaRegistryClient, FakeSchemaRegistryClient>();
        var client = (FakeSchemaRegistryClient)proxy!;
        const string subject = "topic-key";
        const string schema = "{\"type\":\"record\",\"name\":\"CompositeKey\",\"fields\":[{\"name\":\"id\",\"type\":\"int\"},{\"name\":\"code\",\"type\":\"string\"}]}";
        client.SchemaString = schema;
        client.RegisteredSchemaStrings[subject] = schema;
        var ser = new AvroCompositeKeySerializer(proxy, subject, 1);
        var des = new AvroCompositeKeyDeserializer(proxy, subject);
        var dict = new Dictionary<string, object> { ["id"] = 1, ["code"] = "a" };
        var ctx = new SerializationContext(MessageComponentType.Key, "topic");
        var bytes = ser.Serialize(dict, ctx);
        var result = (Dictionary<string, object>)des.Deserialize(bytes, false, ctx);
        Assert.Equal(dict["id"], result["id"]);
        Assert.Equal(dict["code"], result["code"]);
    }

    [Fact]
    public void AvroCompositeKeySerializer_WrongType_Throws()
    {
        var proxy = DispatchProxy.Create<ISchemaRegistryClient, FakeSchemaRegistryClient>();
        var client = (FakeSchemaRegistryClient)proxy!;
        const string subject = "t-key";
        client.SchemaString = "{\"type\":\"record\",\"name\":\"CompositeKey\",\"fields\":[]}";
        var ser = new AvroCompositeKeySerializer(proxy, subject, 1);
        Assert.Throws<InvalidOperationException>(() => ser.Serialize(123, new SerializationContext(MessageComponentType.Key, "t")));
    }

    [Fact]
    public void AvroCompositeKeyDeserializer_IsNull_ReturnsEmpty()
    {
        var proxy = DispatchProxy.Create<ISchemaRegistryClient, FakeSchemaRegistryClient>();
        var client = (FakeSchemaRegistryClient)proxy!;
        const string subject = "t-key";
        client.SchemaString = "{\"type\":\"record\",\"name\":\"CompositeKey\",\"fields\":[]}";
        var des = new AvroCompositeKeyDeserializer(proxy, subject);
        var ctx = new SerializationContext(MessageComponentType.Key, "t");
        var result = (Dictionary<string, object>)des.Deserialize(ReadOnlySpan<byte>.Empty, true, ctx);
        Assert.Empty(result);
    }

    [Fact]
    public void AvroDeserializer_ThrowsOnDeserialize()
    {
        var d = new AvroDeserializer<SampleEntity>();
        Assert.Throws<NotSupportedException>(() => d.Deserialize(ReadOnlySpan<byte>.Empty));
        d.Dispose();
    }

    [Fact]
    public void AvroDeserializer_Ctor_DoesNotThrow()
    {
        using var d = new AvroDeserializer<SampleEntity>();
        Assert.NotNull(d);
    }
}

