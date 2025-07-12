using System;
using System.Collections.Generic;

namespace Kafka.Ksql.Linq.Window.Finalization;

internal class WindowState<T> where T : class
{
    public DateTime WindowStart { get; set; }
    public DateTime WindowEnd { get; set; }
    public int WindowMinutes { get; set; }
    public List<T> Events { get; set; } = new();
    public bool IsFinalized { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    public readonly object Lock = new object();
}
