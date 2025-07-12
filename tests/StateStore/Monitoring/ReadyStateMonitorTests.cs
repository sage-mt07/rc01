using Confluent.Kafka;
using Kafka.Ksql.Linq.StateStore.Monitoring;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

#nullable enable

namespace Kafka.Ksql.Linq.Tests.StateStore.Monitoring;

public class ReadyStateMonitorTests
{
    private class DummyConsumer : IConsumer<object, object>
    {
        public List<TopicPartition> Assignment { get; } = new();
        public void Assign(TopicPartition partition) { }
        public void Assign(TopicPartitionOffset partition) { }
        public void Assign(IEnumerable<TopicPartitionOffset> partitions) { }
        public void Assign(IEnumerable<TopicPartition> partitions) { }
        public void IncrementalAssign(IEnumerable<TopicPartitionOffset> partitions) { }
        public void IncrementalAssign(IEnumerable<TopicPartition> partitions) { }
        public void IncrementalUnassign(IEnumerable<TopicPartitionOffset> partitions) { }
        public void IncrementalUnassign(IEnumerable<TopicPartition> partitions) { }
        public void Close() { }
        public void Dispose() { }
        public int AddBrokers(string brokers) => 0;
        public void Pause(IEnumerable<TopicPartition> partitions) { }
        public int Poll(TimeSpan timeout) => 0;
        public ConsumeResult<object, object>? Consume(CancellationToken cancellationToken = default) => null;
        public ConsumeResult<object, object>? Consume(TimeSpan timeout) => null;
        public ConsumeResult<object, object>? Consume(int millisecondsTimeout) => null;
        public void Resume(IEnumerable<TopicPartition> partitions) { }
        public void Seek(TopicPartitionOffset tpo) { }
        public WatermarkOffsets QueryWatermarkOffsets(TopicPartition topicPartition, TimeSpan timeout) => new WatermarkOffsets(0, 0);
        public Offset Position(TopicPartition partition) => new Offset(0);
        public List<string> Subscription => new();
        public void StoreOffset(ConsumeResult<object, object> result) { }
        public void StoreOffset(TopicPartitionOffset offset) { }
        public List<TopicPartitionOffset> OffsetsForTimes(IEnumerable<TopicPartitionTimestamp> timestamps, TimeSpan timeout) => new();
        public WatermarkOffsets GetWatermarkOffsets(TopicPartition partition) => new WatermarkOffsets(0, 0);
        public List<TopicPartitionOffset> Committed(IEnumerable<TopicPartition> partitions, TimeSpan timeout) => new();
        public List<TopicPartitionOffset> Committed(TimeSpan timeout) => new();
        public void Subscribe(string topic) { }
        public void Subscribe(IEnumerable<string> topics) { }
        public void Unassign() { }
        public void Unsubscribe() { }
        public string MemberId => "";
        public Handle Handle => throw new NotImplementedException();
        public string Name => "dummy";
        public IConsumerGroupMetadata ConsumerGroupMetadata => null!;
        public void SetSaslCredentials(string username, string password) { }
        public event EventHandler<Error>? Error;
        public void OnError(Error error) => Error?.Invoke(this, error);
        public List<TopicPartitionOffset> Commit() => new();
        public void Commit(ConsumeResult<object, object> result) { }
        public void Commit(IEnumerable<TopicPartitionOffset> offsets) { }
    }

    private class TestMonitor : ReadyStateMonitor
    {
        public TestMonitor() : base(new DummyConsumer(), "t", NullLoggerFactory.Instance) { }
        public void TriggerReady() => typeof(ReadyStateMonitor).GetMethod("OnReadyStateChanged", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(this, new object[] { new ReadyStateChangedEventArgs { TopicName = "t", IsReady = true } });
    }

    [Fact]
    public async Task WaitUntilReadyAsync_CompletesWhenTriggered()
    {
        var monitor = new TestMonitor();
        var task = monitor.WaitUntilReadyAsync(TimeSpan.FromSeconds(1));
        monitor.TriggerReady();
        var result = await task;
        Assert.True(result);
    }
}
