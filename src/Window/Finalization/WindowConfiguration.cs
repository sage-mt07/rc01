using System;
using System.Collections.Generic;

namespace Kafka.Ksql.Linq.Window.Finalization;

public class WindowConfiguration<T> where T : class
{
    public string TopicName { get; set; } = string.Empty;
    public int[] Windows { get; set; } = Array.Empty<int>();
    public TimeSpan GracePeriod { get; set; } = TimeSpan.FromSeconds(3);
    public int RetentionHours { get; set; } = 24;
    public Func<List<T>, object> AggregationFunc { get; set; } = events => events.Count;
    public IKafkaProducer FinalTopicProducer { get; set; } = null!;

    public string GetFinalTopicName(int windowMinutes)
    {
        return $"{TopicName}_window_{windowMinutes}_final";
    }
}
