namespace Kafka.Ksql.Linq.Messaging.Abstractions;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public interface IKafkaConsumer<TKey, TValue> : IDisposable where TValue : class where TKey : notnull
{
    IAsyncEnumerable<KafkaMessage<TValue, TKey>> ConsumeAsync(CancellationToken cancellationToken = default);
    Task<KafkaBatch<TValue, TKey>> ConsumeBatchAsync(KafkaBatchOptions options, CancellationToken cancellationToken = default);
    Task CommitAsync();
    Task SeekAsync(TopicPartitionOffset offset);
    List<TopicPartition> GetAssignedPartitions();
    string TopicName { get; }
}
