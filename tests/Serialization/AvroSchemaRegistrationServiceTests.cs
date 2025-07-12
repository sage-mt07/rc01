using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using ConfluentClient = Confluent.SchemaRegistry.ISchemaRegistryClient;
using Confluent.SchemaRegistry;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Serialization.Abstractions;
using Kafka.Ksql.Linq.Serialization.Avro.Core;
using Kafka.Ksql.Linq.Serialization.Avro.Management;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Serialization;

public class AvroSchemaRegistrationServiceTests
{
    private class Sample
    {
        public int Id { get; set; }
    }

    private class OrderValue
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    private static (AvroSchemaRegistrationService svc, FakeSchemaRegistryClient fake) CreateService()
    {
        var proxy = DispatchProxy.Create<ConfluentClient, FakeSchemaRegistryClient>();
        var fake = (FakeSchemaRegistryClient)proxy!;
        var svc = new AvroSchemaRegistrationService(proxy, new NullLoggerFactory());
        return (svc, fake);
    }

    [Fact]
    public void Constructor_NullClient_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new AvroSchemaRegistrationService(null!, null));
    }

    [Fact]
    public async Task RegisterAllSchemasAsync_RegistersAndStores()
    {
        var (svc, fake) = CreateService();
        var cfg = new AvroEntityConfiguration(typeof(Sample));
        var map = new Dictionary<Type, AvroEntityConfiguration> { { typeof(Sample), cfg } };
        await svc.RegisterAllSchemasAsync(map);
        Assert.Contains("sample-key", fake.RegisterSubjects);
        Assert.Contains("sample-value", fake.RegisterSubjects);
        var info = await svc.GetSchemaInfoAsync<Sample>();
        Assert.Equal(typeof(Sample), info.EntityType);
    }

    [Fact]
    public async Task GetAllRegisteredSchemasAsync_ReturnsList()
    {
        var (svc, _) = CreateService();
        var cfg = new AvroEntityConfiguration(typeof(Sample));
        await svc.RegisterAllSchemasAsync(new Dictionary<Type, AvroEntityConfiguration> { { typeof(Sample), cfg } });
        var all = await svc.GetAllRegisteredSchemasAsync();
        Assert.Single(all);
    }

    [Fact]
    public async Task RegisteredSchema_EqualsGeneratedSchema()
    {
        var proxy = DispatchProxy.Create<ConfluentClient, FakeSchemaRegistryClient>();
        var fake = (FakeSchemaRegistryClient)proxy!;
        var svc = new AvroSchemaRegistrationService(proxy, new NullLoggerFactory());

        var cfg = new AvroEntityConfiguration(typeof(OrderValue));
        await svc.RegisterAllSchemasAsync(new Dictionary<Type, AvroEntityConfiguration> { { typeof(OrderValue), cfg } });

        var registered = fake.RegisteredSchemaStrings["ordervalue-value"];
        var generated = UnifiedSchemaGenerator.GenerateValueSchema(typeof(OrderValue), cfg);
        Assert.Equal(generated, registered);
    }
}
