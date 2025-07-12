using Kafka.Ksql.Linq.StateStore.Core;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Kafka.Ksql.Linq.Window.Finalization;

internal class WindowFinalConsumer : IDisposable
{
    private readonly ILogger<WindowFinalConsumer> _logger;
    private readonly ConcurrentDictionary<string, WindowFinalMessage> _finalizedWindows = new();
    private readonly RocksDbStateStore<string, WindowFinalMessage> _rocksDbStore;
    private bool _disposed = false;

    public WindowFinalConsumer(
        RocksDbStateStore<string, WindowFinalMessage> rocksDbStore,
        ILoggerFactory? loggerFactory = null)
    {
        _rocksDbStore = rocksDbStore ?? throw new ArgumentNullException(nameof(rocksDbStore));
        _logger = loggerFactory?.CreateLogger<WindowFinalConsumer>()
                 ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<WindowFinalConsumer>.Instance;
    }

    /// <summary>
    /// Subscribe to finalized messages and persist them in RocksDB
    /// </summary>
    public async Task SubscribeToFinalizedWindows(string topicName,
        int windowMinutes,
        Func<WindowFinalMessage, Task> messageHandler)
    {
        _logger.LogInformation("Starting subscription to finalized windows: {Topic}({Window}) → RocksDB",
            topicName, windowMinutes);

        var finalTopic = $"{topicName}_window_{windowMinutes}_final";

        // Kafka Consumer setup for final topic
        // await foreach (var message in kafkaConsumer.ConsumeAsync())
        // {
        //     await HandleFinalizedWindowWithRocksDB(message, messageHandler);
        // }

        await Task.CompletedTask; // placeholder implementation
    }

    /// <summary>
    /// Handle a finalized message with deduplication and RocksDB persistence
    /// </summary>
    private async Task HandleFinalizedWindowWithRocksDB(WindowFinalMessage message,
        Func<WindowFinalMessage, Task> messageHandler)
    {
        // Deduplicate: keep the first message for the same key
        if (_finalizedWindows.TryAdd(message.WindowKey, message))
        {
            _logger.LogDebug("Processing new finalized window: {WindowKey} from POD: {PodId}",
                message.WindowKey, message.PodId);

            // Save to RocksDB
            _rocksDbStore.Put(message.WindowKey, message);

            // Execute external handler
            await messageHandler(message);
        }
        else
        {
            var existingMessage = _finalizedWindows[message.WindowKey];
            _logger.LogDebug("Duplicate finalized window ignored: {WindowKey}. " +
                           "Existing from POD: {ExistingPod}, Duplicate from POD: {DuplicatePod}",
                message.WindowKey, existingMessage.PodId, message.PodId);
        }
    }

    /// <summary>
    /// Retrieve historical finalized data, preferring RocksDB
    /// </summary>
    public WindowFinalMessage? GetFinalizedWindow(string windowKey)
    {
        // Check the in-memory cache first
        if (_finalizedWindows.TryGetValue(windowKey, out var cachedWindow))
        {
            return cachedWindow;
        }

        // Retrieve from RocksDB
        var persistedWindow = _rocksDbStore.Get(windowKey);
        if (persistedWindow != null)
        {
            // Store in memory cache as well
            _finalizedWindows.TryAdd(windowKey, persistedWindow);
            return persistedWindow;
        }

        return null;
    }

    /// <summary>
    /// Get finalized data within a date range using RocksDB search
    /// </summary>
    public List<WindowFinalMessage> GetFinalizedWindowsInRange(DateTime start, DateTime end)
    {
        var results = new List<WindowFinalMessage>();

        // Retrieve all data from RocksDB then filter by the range
        foreach (var kvp in _rocksDbStore.All())
        {
            var window = kvp.Value;
            if (window.WindowStart >= start && window.WindowEnd <= end)
            {
                results.Add(window);
            }
        }

        return results.OrderBy(w => w.WindowStart).ToList();
    }

    /// <summary>
    /// Get finalized data for a specific window size
    /// </summary>
    public List<WindowFinalMessage> GetFinalizedWindowsBySize(int windowMinutes, DateTime? since = null)
    {
        var results = new List<WindowFinalMessage>();
        var cutoffTime = since ?? DateTime.UtcNow.AddDays(-7); // default is seven days ago

        foreach (var kvp in _rocksDbStore.All())
        {
            var window = kvp.Value;
            if (window.WindowMinutes == windowMinutes && window.WindowStart >= cutoffTime)
            {
                results.Add(window);
            }
        }

        return results.OrderBy(w => w.WindowStart).ToList();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;

            // Flush RocksDB before disposing
            _rocksDbStore?.Flush();
            _rocksDbStore?.Dispose();

            _finalizedWindows.Clear();
            _logger.LogInformation("WindowFinalConsumer disposed with RocksDB persistence");
        }
    }
}
