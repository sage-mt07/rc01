using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Kafka.Ksql.Linq.Window.Finalization;

public class WindowFinalizationManager : IDisposable
{
    private readonly ILogger<WindowFinalizationManager> _logger;
    private readonly ConcurrentDictionary<string, WindowProcessor> _processors = new();
    private readonly Timer _finalizationTimer;
    private readonly WindowFinalizationOptions _options;
    private bool _disposed = false;

    public WindowFinalizationManager(
        WindowFinalizationOptions options,
        ILoggerFactory? loggerFactory = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = loggerFactory?.CreateLogger<WindowFinalizationManager>()
                 ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<WindowFinalizationManager>.Instance;

        // Periodically finalize windows (default: every second)
        _finalizationTimer = new Timer(ProcessWindowFinalization, null,
            _options.FinalizationInterval, _options.FinalizationInterval);

        _logger.LogInformation("WindowFinalizationManager initialized with interval: {Interval}ms",
            _options.FinalizationInterval.TotalMilliseconds);
    }

    /// <summary>
    /// Register window processing for an entity type
    /// </summary>
    public void RegisterWindowProcessor<T>(WindowConfiguration<T> config) where T : class
    {
        var processorKey = GenerateProcessorKey<T>(config);

        if (_processors.ContainsKey(processorKey))
        {
            _logger.LogDebug("Window processor already registered: {Key}", processorKey);
            return;
        }

        var processor = new WindowProcessor<T>(config, _logger);
        _processors[processorKey] = processor;

        _logger.LogInformation("Registered window processor: {EntityType} -> {Windows}min",
            typeof(T).Name, string.Join(",", config.Windows));
    }

    /// <summary>
    /// Timer-driven window finalization
    /// </summary>
    private void ProcessWindowFinalization(object? state)
    {
        if (_disposed) return;

        var now = DateTime.UtcNow;
        _logger.LogTrace("Processing window finalization at {Timestamp}", now);

        var finalizationTasks = _processors.Values
            .Select(processor => processor.ProcessFinalization(now))
            .ToArray();

        Task.WaitAll(finalizationTasks, TimeSpan.FromSeconds(30));
    }

    private string GenerateProcessorKey<T>(WindowConfiguration<T> config) where T : class
    {
        return $"{typeof(T).FullName}_{config.TopicName}";
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _finalizationTimer?.Dispose();

            foreach (var processor in _processors.Values)
            {
                processor?.Dispose();
            }
            _processors.Clear();

            _logger.LogInformation("WindowFinalizationManager disposed");
        }
    }
}
