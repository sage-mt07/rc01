using System;

namespace Kafka.Ksql.Linq.Window.Finalization;

public class WindowFinalizationOptions
{
    public TimeSpan FinalizationInterval { get; set; } = TimeSpan.FromSeconds(1);
    public int MaxConcurrentFinalizations { get; set; } = 10;
    public bool EnableDetailedLogging { get; set; } = false;
}
