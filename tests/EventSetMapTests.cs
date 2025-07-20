using Kafka.Ksql.Linq.Core.Modeling;
using Kafka.Ksql.Linq;
using Kafka.Ksql.Linq.Core.Abstractions;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Kafka.Ksql.Linq.Tests;

public class EventSetMapTests
{
    private class DummyContext : IKsqlContext
    {
        public IEntitySet<T> Set<T>() where T : class => throw new NotImplementedException();
        public object GetEventSet(Type entityType) => throw new NotImplementedException();
        public Dictionary<Type, EntityModel> GetEntityModels() => new();
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private class Sample
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private class SampleDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private class SampleSet : EventSet<Sample>
    {
        private readonly List<Sample> _items;

        public SampleSet(List<Sample> items, EntityModel model) : base(new DummyContext(), model)
        {
            _items = items;
        }

        protected override Task SendEntityAsync(Sample entity, Dictionary<string, string>? headers, CancellationToken cancellationToken) => Task.CompletedTask;

        public override async IAsyncEnumerator<Sample> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            foreach (var item in _items)
            {
                if (cancellationToken.IsCancellationRequested)
                    yield break;

                yield return item;
                await Task.Yield();
            }
        }
    }

    private static EntityModel CreateModel()
    {
        var builder = new ModelBuilder();
        builder.Entity<Sample>()
            .WithTopic("sample-topic")
            .HasKey(e => e.Id);
        return builder.GetEntityModel<Sample>()!;
    }

    [Fact]
    public async Task Map_ForEachAsync_ReturnsMappedValues()
    {
        var items = new List<Sample> { new Sample { Id = 1, Name = "A" } };
        var set = new SampleSet(items, CreateModel());

        var mapped = set.Map(x => new SampleDto { Id = x.Id, Name = x.Name });

        var results = new List<SampleDto>();
        await mapped.ForEachAsync(e => { results.Add(e); return Task.CompletedTask; });

        var dto = Assert.Single(results);
        Assert.Equal(1, dto.Id);
        Assert.Equal("A", dto.Name);
    }
}
