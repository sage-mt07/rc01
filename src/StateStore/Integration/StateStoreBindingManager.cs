using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Messaging.Consumers;
using Kafka.Ksql.Linq.StateStore.Core;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Kafka.Ksql.Linq.StateStore.Integration;

internal class StateStoreBindingManager : IDisposable
{
    private readonly List<IDisposable> _bindings = new();
    private readonly Dictionary<string, BindingHealthStatus> _healthStatuses = new();
    private readonly ILogger<StateStoreBindingManager> _logger;
    private readonly Timer? _healthCheckTimer;
    private readonly object _lock = new();

    internal StateStoreBindingManager(ILoggerFactory? loggerFactory = null)
    {
        _logger = loggerFactory?.CreateLogger<StateStoreBindingManager>()
                 ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<StateStoreBindingManager>.Instance;

        // 30秒ごとにヘルスチェック
        _healthCheckTimer = new Timer(PerformHealthCheck, null,
            TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    internal async Task<TopicStateStoreBinding<T>> CreateBindingAsync<T>(
        IStateStore<string, T> stateStore,
        KafkaConsumerManager consumerManager,
        EntityModel entityModel,
        ILoggerFactory? loggerFactory = null) where T : class
    {
        var binding = new TopicStateStoreBinding<T>(stateStore, consumerManager, entityModel, loggerFactory);

        try
        {
            await binding.StartBindingAsync();

            lock (_lock)
            {
                _bindings.Add(binding);
                var status = binding.GetHealthStatus();
                _healthStatuses[status.TopicName] = status;
            }

            _logger.LogInformation("Created StateStore binding for {EntityType}", typeof(T).Name);
            return binding;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create binding for {EntityType}", typeof(T).Name);
            binding.Dispose();
            throw;
        }
    }

    private void PerformHealthCheck(object? state)
    {
        lock (_lock)
        {
            var unhealthyBindings = new List<string>();

            foreach (var binding in _bindings.OfType<TopicStateStoreBinding<object>>())
            {
                var status = binding.GetHealthStatus();
                _healthStatuses[status.TopicName] = status;

                if (!status.IsHealthy)
                {
                    unhealthyBindings.Add(status.TopicName);
                    _logger.LogWarning("Unhealthy binding detected: {Status}", status);
                }
            }

            if (unhealthyBindings.Count > 0)
            {
                _logger.LogWarning("Found {Count} unhealthy bindings: {Topics}",
                    unhealthyBindings.Count, string.Join(", ", unhealthyBindings));
            }
        }
    }

    internal Dictionary<string, BindingHealthStatus> GetAllHealthStatuses()
    {
        lock (_lock)
        {
            return new Dictionary<string, BindingHealthStatus>(_healthStatuses);
        }
    }

    public void Dispose()
    {
        _healthCheckTimer?.Dispose();

        _logger.LogInformation("Disposing {BindingCount} StateStore bindings", _bindings.Count);

        lock (_lock)
        {
            foreach (var binding in _bindings)
            {
                binding?.Dispose();
            }
            _bindings.Clear();
            _healthStatuses.Clear();
        }
    }
}
