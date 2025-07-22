using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Kafka.Ksql.Linq.Window.Finalization;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Window.Finalization;

public class WindowFinalizationManagerTests
{
    private class FakeProducer : IKafkaProducer
    {
        public List<(string Topic,string Key,object Value)> Sent { get; } = new();
        public Task SendAsync(string topic, string key, object value)
        {
            Sent.Add((topic,key,value));
            return Task.CompletedTask;
        }
        public void Dispose() { }
    }

    private class TestEntity
    {
        public int Id { get; set; }
    }

    [Fact]
    public async Task FinalTopicName_IncludesWindowSize()
    {
        var producer = new FakeProducer();
        var config = new WindowConfiguration<TestEntity>
        {
            TopicName = "orders",
            Windows = new[] { 5 },
            GracePeriod = TimeSpan.Zero,
            FinalTopicProducer = producer,
            AggregationFunc = events => events.Count
        };
        var processor = new WindowProcessor<TestEntity>(config, NullLogger.Instance);

        var now = DateTime.UtcNow;
        processor.AddToWindow(new TestEntity { Id = 1 }, now);

        await processor.ProcessFinalization(now.AddMinutes(6));

        Assert.Contains(producer.Sent, x => x.Topic == "orders_window_5_final");
    }

    [Fact]
    public async Task FinalTopicNames_ForMultipleWindowSizes()
    {
        var producer = new FakeProducer();
        var config = new WindowConfiguration<TestEntity>
        {
            TopicName = "orders",
            Windows = new[] { 5, 10, 60 },
            GracePeriod = TimeSpan.Zero,
            FinalTopicProducer = producer,
            AggregationFunc = events => events.Count
        };

        var processor = new WindowProcessor<TestEntity>(config, NullLogger.Instance);

        var now = DateTime.UtcNow;
        processor.AddToWindow(new TestEntity { Id = 1 }, now);

        await processor.ProcessFinalization(now.AddMinutes(61));

        Assert.Contains(producer.Sent, x => x.Topic == "orders_window_5_final");
        Assert.Contains(producer.Sent, x => x.Topic == "orders_window_10_final");
        Assert.Contains(producer.Sent, x => x.Topic == "orders_window_60_final");
    }

    [Fact]
    public async Task FinalizationManager_TriggersProcessorViaTimer()
    {
        var producer = new FakeProducer();
        var config = new WindowConfiguration<TestEntity>
        {
            TopicName = "orders",
            Windows = new[] { 5 },
            GracePeriod = TimeSpan.Zero,
            FinalTopicProducer = producer,
            AggregationFunc = events => events.Count
        };

        var options = new WindowFinalizationOptions
        {
            FinalizationInterval = TimeSpan.FromMilliseconds(50)
        };

        using var manager = new WindowFinalizationManager(options);
        manager.RegisterWindowProcessor(config);

        // access internal processor to add events
        var processorsField = typeof(WindowFinalizationManager)
            .GetField("_processors", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var dict = (System.Collections.Concurrent.ConcurrentDictionary<string, WindowProcessor>)processorsField.GetValue(manager)!;
        var processor = (WindowProcessor<TestEntity>)dict.Values.First();

        var past = DateTime.UtcNow.AddMinutes(-6);
        processor.AddToWindow(new TestEntity { Id = 1 }, past);

        await Task.Delay(150);

        Assert.Contains(producer.Sent, x => x.Topic == "orders_window_5_final");
    }

    [Fact]
    public async Task GeneratesEmptyWindows_WhenNoEvents()
    {
        var producer = new FakeProducer();
        var config = new WindowConfiguration<TestEntity>
        {
            TopicName = "orders",
            Windows = new[] { 5 },
            GracePeriod = TimeSpan.Zero,
            FinalTopicProducer = producer,
            AggregationFunc = events => events.Count
        };

        var processor = new WindowProcessor<TestEntity>(config, NullLogger.Instance);

        var field = typeof(WindowProcessor<TestEntity>).GetField("_nextEmptyWindowStart", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var dict = (ConcurrentDictionary<int, DateTime>)field.GetValue(processor)!;
        dict[5] = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        await processor.ProcessFinalization(new DateTime(2020, 1, 1, 0, 12, 0, DateTimeKind.Utc));

        Assert.True(producer.Sent.Count >= 2);
        Assert.All(producer.Sent, x => Assert.Equal(0, ((WindowFinalMessage)x.Value).EventCount));
    }
}
