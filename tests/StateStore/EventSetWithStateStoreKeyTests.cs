using Kafka.Ksql.Linq;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.StateStore;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

#nullable enable

namespace Kafka.Ksql.Linq.Tests.StateStore;

public class EventSetWithStateStoreKeyTests
{
    private class DummyContext : IKsqlContext
    {
        private class StubSet<T> : IEntitySet<T> where T : class
        {
            public Task AddAsync(T entity, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task<List<T>> ToListAsync(CancellationToken cancellationToken = default) => Task.FromResult(new List<T>());
            public Task ForEachAsync(Func<T, Task> action, TimeSpan timeout = default, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public string GetTopicName() => typeof(T).Name;
            public EntityModel GetEntityModel() => new EntityModel { EntityType = typeof(T), TopicName = "t", AllProperties = Array.Empty<System.Reflection.PropertyInfo>(), KeyProperties = Array.Empty<System.Reflection.PropertyInfo>() };
            public IKsqlContext GetContext() => null!;
            public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) { await Task.CompletedTask; yield break; }
        }

        public IEntitySet<T> Set<T>() where T : class => new StubSet<T>();
        public object GetEventSet(Type entityType) => throw new NotImplementedException();
        public Dictionary<Type, EntityModel> GetEntityModels() => new();
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private class Sample
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public int PartA { get; set; }
        public int PartB { get; set; }
    }

    private static EntityModel ModelWithKeys(params string[] keyNames)
    {
        var all = typeof(Sample).GetProperties();
        var keys = new List<System.Reflection.PropertyInfo>();
        foreach (var name in keyNames)
        {
            keys.Add(typeof(Sample).GetProperty(name)!);
        }
        return new EntityModel
        {
            EntityType = typeof(Sample),
            TopicName = "t",
            AllProperties = all,
            KeyProperties = keys.ToArray()
        };
    }

    private static string GenerateKey(EntityModel model, Sample? entity)
    {
        var set = new EventSetWithStateStore<Sample>(new DummyContext(), model);
        return PrivateAccessor.InvokePrivate<string>(set, "GenerateEntityKey", new[] { typeof(Sample) }, args: new object?[] { entity });
    }

    [Fact]
    public void GenerateEntityKey_NullEntity_ReturnsGuidLike()
    {
        var model = ModelWithKeys("Id");
        var key = GenerateKey(model, null);
        Assert.False(string.IsNullOrEmpty(key));
        Guid.Parse(key); // should be valid guid
    }

    [Fact]
    public void GenerateEntityKey_NoKeyProperties_UsesHashCode()
    {
        var model = ModelWithKeys();
        var entity = new Sample { Id = 1 };
        var key = GenerateKey(model, entity);
        Assert.Equal(entity.GetHashCode().ToString(), key);
    }

    [Fact]
    public void GenerateEntityKey_SingleKeyProperty_UsesValue()
    {
        var model = ModelWithKeys("Id");
        var entity = new Sample { Id = 123 };
        var key = GenerateKey(model, entity);
        Assert.Equal("123", key);
    }

    [Fact]
    public void GenerateEntityKey_MultipleKeys_ConcatenatesValues()
    {
        var model = ModelWithKeys("PartA", "PartB");
        var entity = new Sample { PartA = 1, PartB = 2 };
        var key = GenerateKey(model, entity);
        Assert.Equal("1|2", key);
    }
}
