using Kafka.Ksql.Linq.Core.Abstractions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
namespace Kafka.Ksql.Linq.Window.Finalization;

internal class WindowProcessor<T> : WindowProcessor where T : class
{
    private readonly WindowConfiguration<T> _config;
    private readonly ConcurrentDictionary<string, WindowState<T>> _windowStates = new();
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<int, DateTime> _nextEmptyWindowStart = new();

    public WindowProcessor(WindowConfiguration<T> config, ILogger logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var now = DateTime.UtcNow;
        foreach (var windowMinutes in _config.Windows)
        {
            var start = CalculateWindowStart(now, windowMinutes).AddMinutes(-windowMinutes);
            _nextEmptyWindowStart[windowMinutes] = start;
        }
    }

    /// <summary>
    /// Add new data to its corresponding window
    /// </summary>
    public void AddToWindow(T entity, DateTime eventTime)
    {
        foreach (var windowMinutes in _config.Windows)
        {
            var windowKey = GenerateWindowKey(entity, eventTime, windowMinutes);
            var windowStart = CalculateWindowStart(eventTime, windowMinutes);
            var windowEnd = windowStart.AddMinutes(windowMinutes);

            var windowState = _windowStates.GetOrAdd(windowKey, _ => new WindowState<T>
            {
                WindowStart = windowStart,
                WindowEnd = windowEnd,
                WindowMinutes = windowMinutes,
                Events = new List<T>(),
                IsFinalized = false
            });

            lock (windowState.Lock)
            {
                if (!windowState.IsFinalized)
                {
                    windowState.Events.Add(entity);
                    windowState.LastUpdated = DateTime.UtcNow;

                    _logger.LogTrace("Added event to window: {WindowKey}, Events: {Count}",
                        windowKey, windowState.Events.Count);
                }
            }
        }
    }

    /// <summary>
    /// Window確定処理
    /// </summary>
    public override async Task ProcessFinalization(DateTime currentTime)
    {
        EnsureEmptyWindows(currentTime);

        var windowsToFinalize = new List<(string key, WindowState<T> state)>();

        // Identify windows ready for finalization
        foreach (var kvp in _windowStates)
        {
            var windowState = kvp.Value;
            if (!windowState.IsFinalized && ShouldFinalizeWindow(windowState, currentTime))
            {
                windowsToFinalize.Add((kvp.Key, windowState));
            }
        }

        // Execute finalization
        foreach (var (windowKey, windowState) in windowsToFinalize)
        {
            await FinalizeWindow(windowKey, windowState);
        }

        // Clean up old windows
        await CleanupOldWindows(currentTime);
    }

    /// <summary>
    /// Determine whether a window should be finalized
    /// </summary>
    private bool ShouldFinalizeWindow(WindowState<T> windowState, DateTime currentTime)
    {
        // Finalize once WindowEnd plus the grace period has elapsed
        var finalizeAt = windowState.WindowEnd.Add(_config.GracePeriod);
        return currentTime >= finalizeAt;
    }

    /// <summary>
    /// Finalize an individual window
    /// </summary>
    private async Task FinalizeWindow(string windowKey, WindowState<T> windowState)
    {
        lock (windowState.Lock)
        {
            if (windowState.IsFinalized)
            {
                return; // Already finalized
            }

            windowState.IsFinalized = true;
        }

        try
        {
            // Generate aggregated data
            var finalizedData = _config.AggregationFunc(windowState.Events);

            // Send to the orders_window_final topic
            await SendToFinalTopic(windowKey, finalizedData, windowState);

            _logger.LogInformation("Finalized window: {WindowKey}, Events: {EventCount}, " +
                                 "Window: {Start} - {End}",
                windowKey, windowState.Events.Count,
                windowState.WindowStart, windowState.WindowEnd);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to finalize window: {WindowKey}", windowKey);

            // Reset the finalized flag so the process can be retried
            lock (windowState.Lock)
            {
                windowState.IsFinalized = false;
            }
        }
    }

    /// <summary>
    /// Publish the finalized result to the final topic
    /// </summary>
    private async Task SendToFinalTopic(string windowKey, object finalizedData, WindowState<T> windowState)
    {
        var finalTopicMessage = new WindowFinalMessage
        {
            WindowKey = windowKey,
            WindowStart = windowState.WindowStart,
            WindowEnd = windowState.WindowEnd,
            WindowMinutes = windowState.WindowMinutes,
            EventCount = windowState.Events.Count,
            AggregatedData = finalizedData,
            FinalizedAt = DateTime.UtcNow,
            PodId = Environment.MachineName // Used for POD identification
        };

        // Send using KafkaProducer
        // Deduplicate: if the same key arrives multiple times, keep the first
        var finalTopic = _config.GetFinalTopicName(windowState.WindowMinutes);

        await _config.FinalTopicProducer.SendAsync(
            topic: finalTopic,
            key: windowKey,
            value: finalTopicMessage);

        _logger.LogDebug("Sent finalized window to topic: {Topic}, Key: {Key}",
            finalTopic, windowKey);
    }

    /// <summary>
    /// Remove expired windows from memory
    /// </summary>
    private async Task CleanupOldWindows(DateTime currentTime)
    {
        var cleanupThreshold = currentTime.AddHours(-_config.RetentionHours);
        var keysToRemove = new List<string>();

        foreach (var kvp in _windowStates)
        {
            var windowState = kvp.Value;
            if (windowState.IsFinalized && windowState.WindowEnd < cleanupThreshold)
            {
                keysToRemove.Add(kvp.Key);
            }
        }

        foreach (var key in keysToRemove)
        {
            _windowStates.TryRemove(key, out _);
        }

        if (keysToRemove.Count > 0)
        {
            _logger.LogDebug("Cleaned up {Count} old windows for entity {EntityType}",
                keysToRemove.Count, typeof(T).Name);
        }

        await Task.CompletedTask;
    }

    private string GenerateWindowKey(T entity, DateTime eventTime, int windowMinutes)
    {
        var windowStart = CalculateWindowStart(eventTime, windowMinutes);
        var entityKey = ExtractEntityKey(entity);
        return $"{entityKey}_{windowStart:yyyyMMddHHmm}_{windowMinutes}min";
    }

    private DateTime CalculateWindowStart(DateTime eventTime, int windowMinutes)
    {
        var totalMinutes = eventTime.Hour * 60 + eventTime.Minute;
        var windowStartMinutes = (totalMinutes / windowMinutes) * windowMinutes;
        var hours = windowStartMinutes / 60;
        var minutes = windowStartMinutes % 60;

        return new DateTime(eventTime.Year, eventTime.Month, eventTime.Day, hours, minutes, 0);
    }

    private string ExtractEntityKey(T entity)
    {
        // Build a unique key from the entity's key properties
        if (entity == null) return Guid.NewGuid().ToString();

        return entity.GetHashCode().ToString();
    }

    private void EnsureEmptyWindows(DateTime currentTime)
    {
        foreach (var windowMinutes in _config.Windows)
        {
            var nextStart = _nextEmptyWindowStart[windowMinutes];
            var cutoff = CalculateWindowStart(currentTime, windowMinutes);

            while (nextStart <= cutoff)
            {
                var windowKey = $"empty_{nextStart:yyyyMMddHHmm}_{windowMinutes}min";
                _windowStates.GetOrAdd(windowKey, _ => new WindowState<T>
                {
                    WindowStart = nextStart,
                    WindowEnd = nextStart.AddMinutes(windowMinutes),
                    WindowMinutes = windowMinutes,
                    Events = new List<T>(),
                    IsFinalized = false
                });

                nextStart = nextStart.AddMinutes(windowMinutes);
            }

            _nextEmptyWindowStart[windowMinutes] = nextStart;
        }
    }

    public override void Dispose()
    {
        _windowStates.Clear();
    }
}

internal abstract class WindowProcessor : IDisposable
{
    public abstract Task ProcessFinalization(DateTime currentTime);
    public abstract void Dispose();
}
