using System;

namespace Kafka.Ksql.Linq.Serialization.Abstractions;

public class SerializationStatistics
{
    public double HitRate => TotalSerializations > 0 ? (double)CacheHits / TotalSerializations : 0.0;
    public TimeSpan AverageLatency { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    public long TotalSerializations;
    public long TotalDeserializations;
    public long CacheHits;
    public long CacheMisses;
}
