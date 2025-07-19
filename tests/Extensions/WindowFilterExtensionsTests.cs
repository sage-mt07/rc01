using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Core.Extensions;
using Kafka.Ksql.Linq.Core.Modeling;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Extensions;

public class WindowFilterExtensionsTests
{
    private class DummySet<T> : IEntitySet<T>
        where T : class
    {
        private readonly List<T> _items = new();
        private readonly EntityModel _model;
        public DummySet(EntityModel model)
        {
            _model = model;
        }
        public void AddItem(T item) => _items.Add(item);
        public Task AddAsync(T entity, CancellationToken cancellationToken = default) { _items.Add(entity); return Task.CompletedTask; }
        public Task RemoveAsync(T entity, CancellationToken cancellationToken = default) { _items.Remove(entity); return Task.CompletedTask; }
        public Task<List<T>> ToListAsync(CancellationToken cancellationToken = default) => Task.FromResult(new List<T>(_items));
        public Task ForEachAsync(Func<T, Task> action, TimeSpan timeout = default, CancellationToken cancellationToken = default) => Task.WhenAll(_items.Select(action));
        public string GetTopicName() => _model.TopicName ?? typeof(T).Name.ToLowerInvariant();
        public EntityModel GetEntityModel() => _model;
        public IKsqlContext GetContext() => throw new NotImplementedException();
        public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            foreach (var i in _items)
            {
                yield return i;
                await Task.Yield();
            }
        }
    }

    private class Sample
    {
        public int Id { get; set; }
        public int WindowMinutes { get; set; }
    }

    private static DummySet<Sample> CreateSet()
    {
        var model = new EntityModel
        {
            EntityType = typeof(Sample),
            TopicName = "sample",
            AllProperties = typeof(Sample).GetProperties(),
            KeyProperties = new[] { typeof(Sample).GetProperty(nameof(Sample.Id))! },
            ValidationResult = new ValidationResult { IsValid = true }
        };
        return new DummySet<Sample>(model);
    }

    [Fact]
    public async Task Window_FiltersByWindowMinutes()
    {
        var set = CreateSet();
        set.AddItem(new Sample { Id = 1, WindowMinutes = 5 });
        set.AddItem(new Sample { Id = 2, WindowMinutes = 1 });

        var filtered = set.Window(5);
        var list = await filtered.ToListAsync();
        Assert.Single(list);
        Assert.Equal(1, list[0].Id);
    }
}
