using System;

namespace Kafka.Ksql.Linq.StateStore.Integration;

internal class BindingHealthStatus
{
    public bool IsRunning { get; set; }
    public bool HasTask { get; set; }
    public string TaskStatus { get; set; } = string.Empty;
    public bool IsDisposed { get; set; }
    public string TopicName { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;
    public long StoreSize { get; set; }

    public bool IsHealthy => IsRunning && HasTask && !IsDisposed && TaskStatus != "Faulted";

    // Ready状態監視情報
    public bool IsReady { get; set; }
    public long CurrentLag { get; set; }
    public TimeSpan? TimeToReady { get; set; }
    public TimeSpan TimeSinceBinding { get; set; }

    public override string ToString()
    {
        return $"Topic: {TopicName}, Store: {StoreName}, Running: {IsRunning}, Size: {StoreSize}, Status: {TaskStatus}";
    }
}
