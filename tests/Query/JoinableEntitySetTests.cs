using Kafka.Ksql.Linq.Core.Modeling;
using Kafka.Ksql.Linq.Query.Linq;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Tests;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

#nullable enable

namespace Kafka.Ksql.Linq.Tests.Query;

public class JoinableEntitySetTests
{
    private class DummyContext : IKsqlContext
    {
        public IEntitySet<T> Set<T>() where T : class => throw new NotImplementedException();
        public object GetEventSet(Type entityType) => throw new NotImplementedException();
        public Dictionary<Type, EntityModel> GetEntityModels() => new();
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private class DummySet<T> : IEntitySet<T> where T : class
    {
        private readonly IKsqlContext _context = new DummyContext();
        public Task AddAsync(T entity, Dictionary<string, string>? headers = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveAsync(T entity, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<List<T>> ToListAsync(CancellationToken cancellationToken = default) => Task.FromResult(new List<T>());
        public Task ForEachAsync(Func<T, Task> action, TimeSpan timeout = default, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ForEachAsync(Func<T, KafkaMessageContext, Task> action, TimeSpan timeout = default, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public string GetTopicName() => typeof(T).Name;
        public EntityModel GetEntityModel()
        {
            var builder = new ModelBuilder();
            builder.Entity<T>()
                .WithTopic(typeof(T).Name.ToLowerInvariant());
            var model = builder.GetEntityModel<T>()!;
            model.ValidationResult = new ValidationResult { IsValid = true };
            return model;
        }
        public IKsqlContext GetContext() => _context;
        public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) { await Task.CompletedTask; yield break; }
    }

    [Fact]
    public async Task Join_CreatesJoinResult_ForValidInputs()
    {
        var outer = new JoinableEntitySet<TestEntity>(new DummySet<TestEntity>());
        var inner = new DummySet<ChildEntity>();

        var result = outer.Join<ChildEntity, object>(inner, o => (object)o.Id, i => (object)i.ParentId)
                          .Select((o, i) => new { o.Id, i.Name });

        var list = await result.ToListAsync();
        Assert.Empty(list);
    }

    [Fact]
    public async Task Join_ThreeWayJoin_WorksCorrectly()
    {
        var outer = new JoinableEntitySet<TestEntity>(new DummySet<TestEntity>());
        var inner = new DummySet<ChildEntity>();
        var third = new DummySet<GrandChildEntity>();

        var result = outer.Join<ChildEntity, object>(inner, o => (object)o.Id, i => (object)i.ParentId)
                          .Join<GrandChildEntity, object>(third, (Expression<Func<TestEntity, object>>)(o => (object)o.Id), g => (object)g.ChildId)
                          .Select((o, i, g) => new { o.Id, g.Description });

        var list = await result.ToListAsync();
        Assert.Empty(list);
    }

    [Fact]
    public void Join_NullArguments_Throws()
    {
        var outer = new JoinableEntitySet<TestEntity>(new DummySet<TestEntity>());
        var inner = new DummySet<ChildEntity>();

        Assert.Throws<ArgumentNullException>(() => outer.Join<ChildEntity, int>(null!, o => o.Id, i => i.ParentId));
        Assert.Throws<ArgumentNullException>(() => outer.Join(inner, null!, i => i.ParentId));
        Assert.Throws<ArgumentNullException>(() => outer.Join(inner, o => o.Id, null!));
    }

    [Fact]
    public void ToString_ReturnsInformation()
    {
        var outer = new JoinableEntitySet<TestEntity>(new DummySet<TestEntity>());
        var str = outer.ToString();
        Assert.Contains("JoinableEntitySet", str);
    }
}
