using Kafka.Ksql.Linq;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Core.Modeling;
using Kafka.Ksql.Linq.Query.Abstractions;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using Xunit;

namespace Kafka.Ksql.Linq.Tests;

public class ForEachAsyncStreamingTests
{
    private class DummyContext : IKsqlContext
    {
        public IEntitySet<T> Set<T>() where T : class => throw new NotImplementedException();
        public object GetEventSet(Type entityType) => throw new NotImplementedException();
        public Dictionary<Type, EntityModel> GetEntityModels() => new();
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private class TestEvent { public int Id { get; set; } }

    private class ChannelEventSet : EventSet<TestEvent>
    {
        private readonly Channel<TestEvent> _channel;
        public ChannelEventSet(Channel<TestEvent> channel) : base(new DummyContext(), CreateModel())
        {
            _channel = channel;
        }
        protected override Task SendEntityAsync(TestEvent entity, Dictionary<string, string>? headers, CancellationToken cancellationToken) => Task.CompletedTask;
        public override IAsyncEnumerator<TestEvent> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new ChannelEnumerator(_channel, cancellationToken);
        }

        private sealed class ChannelEnumerator : IAsyncEnumerator<TestEvent>
        {
            private readonly Channel<TestEvent> _channel;
            private readonly CancellationToken _token;
            public ChannelEnumerator(Channel<TestEvent> channel, CancellationToken token)
            {
                _channel = channel;
                _token = token;
            }

            public TestEvent Current { get; private set; } = null!;

            public async ValueTask<bool> MoveNextAsync()
            {
                try
                {
                    Current = await _channel.Reader.ReadAsync(_token);
                    return true;
                }
                catch (ChannelClosedException)
                {
                    return false;
                }
            }

            public ValueTask DisposeAsync() => default;
        }
        private static EntityModel CreateModel()
        {
            var builder = new ModelBuilder();
            builder.Entity<TestEvent>().WithTopic("t");
            var model = builder.GetEntityModel<TestEvent>()!;
            model.ValidationResult = new ValidationResult { IsValid = true };
            model.SetStreamTableType(StreamTableType.Stream);
            return model;
        }
    }

    [Fact]
    public async Task ForEachAsync_Processes_NewData_Until_Inactivity()
    {
        var channel = Channel.CreateUnbounded<TestEvent>();
        var set = new ChannelEventSet(channel);
        var results = new List<int>();
        var cts = new CancellationTokenSource();

        var task = set.ForEachAsync(e => { results.Add(e.Id); return Task.CompletedTask; }, TimeSpan.FromMilliseconds(200), cts.Token);

        await channel.Writer.WriteAsync(new TestEvent { Id = 1 });
        await Task.Delay(100);
        await channel.Writer.WriteAsync(new TestEvent { Id = 2 });
        await Task.Delay(300);
        await task;

        Assert.Equal(new[] { 1, 2 }, results);
    }

    [Fact]
    public async Task ForEachAsync_Cancels_With_Token()
    {
        var channel = Channel.CreateUnbounded<TestEvent>();
        var set = new ChannelEventSet(channel);
        var cts = new CancellationTokenSource();
        var task = set.ForEachAsync(e => Task.CompletedTask, TimeSpan.FromSeconds(5), cts.Token);

        cts.CancelAfter(100);
        await task;
        Assert.True(cts.IsCancellationRequested);
    }
}
