using Kafka.Ksql.Linq;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Core.Modeling;
using Kafka.Ksql.Linq.Core.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

#nullable enable

namespace Kafka.Ksql.Linq.Tests.Extensions;

public class ScheduleWindowBuilderTests
{
    private class TestEntity
    {
        public int MarketId { get; set; }
        public DateTime Timestamp { get; set; }
    }

    [ScheduleRange(nameof(Open), nameof(Close))]
    private class ScheduleEntity
    {
        public int MarketId { get; set; }
        public DateTime Open { get; set; }
        public DateTime Close { get; set; }
    }

    private class FluentScheduleEntity
    {
        public int MarketId { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
    }

    [Fact]
    public void Window_BaseOn_BuildsMethodCall()
    {
        IQueryable<TestEntity> src = new List<TestEntity>().AsQueryable();

        var query = src.Window().BaseOn<ScheduleEntity>(e => e.MarketId);
        var call = Assert.IsAssignableFrom<MethodCallExpression>(query.Expression);
        Assert.Equal("BaseOnImpl", call.Method.Name);
        var genArgs = call.Method.GetGenericArguments();
        Assert.Equal(typeof(TestEntity), genArgs[0]);
        Assert.Equal(typeof(ScheduleEntity), genArgs[1]);
    }

    [Fact]
    public void BaseOn_NullSelector_Throws()
    {
        IQueryable<TestEntity> src = new List<TestEntity>().AsQueryable();
        Assert.Throws<ArgumentNullException>(() => src.Window().BaseOn<ScheduleEntity>(null!));
    }

    [Fact]
    public void BaseOnImpl_NonEntitySet_Throws()
    {
        IQueryable<TestEntity> src = new List<TestEntity>().AsQueryable();
        Expression<Func<TestEntity, object>> selector = e => e.MarketId;

        var method = typeof(ScheduleWindowBuilder<TestEntity>)
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Single(m => m.Name == "BaseOnImpl")
            .MakeGenericMethod(typeof(TestEntity), typeof(ScheduleEntity));
        var ex = Assert.Throws<TargetInvocationException>(() => method.Invoke(null, new object?[] { src, selector, null, null }));
        Assert.IsType<NotSupportedException>(ex.InnerException);
    }

    [Fact]
    public void BaseOnImpl_FluentScheduleRange_Works()
    {
        var ctx = new DummyContext();
        var eventModel = new EntityModel { EntityType = typeof(TestEntity), AllProperties = typeof(TestEntity).GetProperties(), KeyProperties = Array.Empty<PropertyInfo>(), ValidationResult = new ValidationResult { IsValid = true } };
        var scheduleModel = new EntityModel { EntityType = typeof(FluentScheduleEntity), AllProperties = typeof(FluentScheduleEntity).GetProperties(), KeyProperties = new[] { typeof(FluentScheduleEntity).GetProperty(nameof(FluentScheduleEntity.MarketId))! }, ValidationResult = new ValidationResult { IsValid = true } };
        var events = new DummySet<TestEntity>(new List<TestEntity>(), ctx, eventModel);
        var schedules = new DummySet<FluentScheduleEntity>(new List<FluentScheduleEntity>(), ctx, scheduleModel);
        ctx.AddSet(events);
        ctx.AddSet(schedules);
        Expression<Func<TestEntity, object>> selector = e => e.MarketId;

        var method = typeof(ScheduleWindowBuilder<TestEntity>)
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Single(m => m.Name == "BaseOnImpl")
            .MakeGenericMethod(typeof(TestEntity), typeof(FluentScheduleEntity));

        var result = method.Invoke(null, new object?[] { events, selector, "Start", "End" });
        Assert.NotNull(result);
    }

    private class DummyContext : IKsqlContext
    {
        private readonly Dictionary<Type, object> _sets = new();
        public void AddSet<T>(DummySet<T> set) where T : class => _sets[typeof(T)] = set;
        public IEntitySet<T> Set<T>() where T : class => (IEntitySet<T>)_sets[typeof(T)];
        public object GetEventSet(Type entityType) => _sets[entityType];
        public Dictionary<Type, EntityModel> GetEntityModels() => new();
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private class DummySet<T> : IQueryable<T>, IEntitySet<T> where T : class
    {
        private readonly IQueryable<T> _query;
        private readonly IKsqlContext _context;
        private readonly EntityModel _model;
        public DummySet(IEnumerable<T> items, IKsqlContext context, EntityModel model)
        {
            _query = items.AsQueryable();
            _context = context;
            _model = model;
        }
        public Type ElementType => _query.ElementType;
        public Expression Expression => _query.Expression;
        public IQueryProvider Provider => _query.Provider;
        public Task AddAsync(T entity, Dictionary<string, string>? headers = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveAsync(T entity, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<List<T>> ToListAsync(CancellationToken cancellationToken = default) => Task.FromResult(_query.ToList());
        public Task ForEachAsync(Func<T, Task> action, TimeSpan timeout = default, CancellationToken cancellationToken = default) => Task.WhenAll(_query.Select(action));
        public string GetTopicName() => _model.TopicName ?? typeof(T).Name.ToLowerInvariant();
        public EntityModel GetEntityModel() => _model;
        public IKsqlContext GetContext() => _context;
        public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            foreach (var i in _query)
            {
                yield return i;
                await Task.Yield();
            }
        }
        public IEnumerator<T> GetEnumerator() => _query.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _query.GetEnumerator();
    }

    [Fact]
    public void BaseOnImpl_MissingScheduleRange_Throws()
    {
        var ctx = new DummyContext();
        var eventModel = new EntityModel { EntityType = typeof(TestEntity), AllProperties = typeof(TestEntity).GetProperties(), KeyProperties = Array.Empty<PropertyInfo>(), ValidationResult = new ValidationResult { IsValid = true } };
        var scheduleModel = new EntityModel { EntityType = typeof(BadSchedule), AllProperties = typeof(BadSchedule).GetProperties(), KeyProperties = new[] { typeof(BadSchedule).GetProperty(nameof(BadSchedule.MarketId))! }, ValidationResult = new ValidationResult { IsValid = true } };
        var events = new DummySet<TestEntity>(new List<TestEntity>(), ctx, eventModel);
        var schedules = new DummySet<BadSchedule>(new List<BadSchedule>(), ctx, scheduleModel);
        ctx.AddSet(events);
        ctx.AddSet(schedules);
        Expression<Func<TestEntity, object>> selector = e => e.MarketId;

        var method = typeof(ScheduleWindowBuilder<TestEntity>)
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Single(m => m.Name == "BaseOnImpl")
            .MakeGenericMethod(typeof(TestEntity), typeof(BadSchedule));
        var ex = Assert.Throws<TargetInvocationException>(() => method.Invoke(null, new object?[] { events, selector, null, null }));
        Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    private class BadSchedule
    {
        public int MarketId { get; set; }
        public DateTime Open { get; set; }
        public DateTime Close { get; set; }
    }
}
