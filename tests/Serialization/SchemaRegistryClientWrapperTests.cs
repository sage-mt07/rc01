using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Confluent.SchemaRegistry;
using Kafka.Ksql.Linq.Serialization.Avro.Core;
using Xunit;

#nullable enable

namespace Kafka.Ksql.Linq.Tests.Serialization;

public class SchemaRegistryClientWrapperTests
{
    private class FakeClient : DispatchProxy
    {
        public bool Disposed { get; private set; }
        public List<string> RegisterSubjects { get; } = new();
        public List<string> CompatibilitySubjects { get; } = new();
        public Func<List<string>> SubjectsProvider { get; set; } = () => new List<string>();
        public Func<List<int>> VersionsProvider { get; set; } = () => new List<int>();
        public Func<int> RegisterReturn { get; set; } = () => 1;
        public Func<bool> CompatibleReturn { get; set; } = () => true;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            switch (targetMethod?.Name)
            {
                case nameof(ISchemaRegistryClient.RegisterSchemaAsync):
                    RegisterSubjects.Add((string)args![0]!);
                    return Task.FromResult(RegisterReturn());
                case nameof(ISchemaRegistryClient.IsCompatibleAsync):
                    CompatibilitySubjects.Add((string)args![0]!);
                    return Task.FromResult(CompatibleReturn());
                case nameof(ISchemaRegistryClient.GetAllSubjectsAsync):
                    return Task.FromResult(SubjectsProvider());
                case nameof(ISchemaRegistryClient.GetSubjectVersionsAsync):
                    return Task.FromResult(VersionsProvider());
                case nameof(IDisposable.Dispose):
                    Disposed = true;
                    return null;
            }
            throw new NotImplementedException(targetMethod?.Name);
        }
    }

    private static (SchemaRegistryClientWrapper wrapper, FakeClient fake) CreateWrapper()
    {
        var proxy = DispatchProxy.Create<ISchemaRegistryClient, FakeClient>();
        var fake = (FakeClient)proxy!;
        var wrapper = new SchemaRegistryClientWrapper(proxy);
        return (wrapper, fake);
    }

    [Fact]
    public void Constructor_NullClient_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new SchemaRegistryClientWrapper(null!));
    }

    [Fact]
    public async Task RegisterKeySchemaAsync_UsesTopicKeySubject()
    {
        var (wrapper, fake) = CreateWrapper();
        var id = await wrapper.RegisterKeySchemaAsync("topic", "schema");
        Assert.Equal(1, id);
        Assert.Contains("topic-key", fake.RegisterSubjects);
    }

    [Fact]
    public async Task RegisterValueSchemaAsync_UsesTopicValueSubject()
    {
        var (wrapper, fake) = CreateWrapper();
        var id = await wrapper.RegisterValueSchemaAsync("topic", "schema");
        Assert.Equal(1, id);
        Assert.Contains("topic-value", fake.RegisterSubjects);
    }

    [Fact]
    public async Task RegisterSchemaAsync_PassesThrough()
    {
        var (wrapper, fake) = CreateWrapper();
        var id = await wrapper.RegisterSchemaAsync("sub", "schema");
        Assert.Equal(1, id);
        Assert.Contains("sub", fake.RegisterSubjects);
    }

    [Fact]
    public async Task RegisterTopicSchemasAsync_CallsBoth()
    {
        var (wrapper, fake) = CreateWrapper();
        await wrapper.RegisterTopicSchemasAsync("t", "ks", "vs");
        Assert.Contains("t-key", fake.RegisterSubjects);
        Assert.Contains("t-value", fake.RegisterSubjects);
    }

    [Fact]
    public async Task CheckCompatibilityAsync_ReturnsClientValue()
    {
        var (wrapper, fake) = CreateWrapper();
        fake.CompatibleReturn = () => false;
        var result = await wrapper.CheckCompatibilityAsync("s", "sc");
        Assert.False(result);
        Assert.Contains("s", fake.CompatibilitySubjects);
    }

    [Fact]
    public async Task GetSchemaVersionsAsync_ReturnsValues()
    {
        var versions = new List<int> { 1, 2 };
        var (wrapper, fake) = CreateWrapper();
        fake.VersionsProvider = () => versions;
        var result = await wrapper.GetSchemaVersionsAsync("subj");
        Assert.Same(versions, result);
    }

    [Fact]
    public async Task GetAllSubjectsAsync_ReturnsValues()
    {
        var subjects = new List<string> { "a", "b" };
        var (wrapper, fake) = CreateWrapper();
        fake.SubjectsProvider = () => subjects;
        var result = await wrapper.GetAllSubjectsAsync();
        Assert.Same(subjects, result);
    }

    [Fact]
    public void Dispose_CallsClientDispose()
    {
        var (wrapper, fake) = CreateWrapper();
        wrapper.Dispose();
        Assert.True(fake.Disposed);
    }

    [Fact]
    public void StringKeySerializer_EncodesUtf8()
    {
        var serializer = new StringKeySerializer();
        var data = serializer.Serialize("abc", new Confluent.Kafka.SerializationContext());
        Assert.Equal(System.Text.Encoding.UTF8.GetBytes("abc"), data);
    }

    [Fact]
    public void StringKeyDeserializer_DecodesUtf8()
    {
        var deserializer = new StringKeyDeserializer();
        var bytes = System.Text.Encoding.UTF8.GetBytes("hello");
        var result = deserializer.Deserialize(bytes, false, new Confluent.Kafka.SerializationContext());
        Assert.Equal("hello", result);
    }

    [Fact]
    public void StringKeyDeserializer_Null_ReturnsEmpty()
    {
        var deserializer = new StringKeyDeserializer();
        var result = deserializer.Deserialize(ReadOnlySpan<byte>.Empty, true, new Confluent.Kafka.SerializationContext());
        Assert.Equal(string.Empty, result);
    }
}
