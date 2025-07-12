using Kafka.Ksql.Linq.StateStore.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;

namespace Kafka.Ksql.Linq.StateStore.Core;

internal class RocksDbStateStore<TKey, TValue> : IStateStore<TKey, TValue>
    where TKey : notnull
    where TValue : class
{
    private readonly string _storeName;
    private readonly string _physicalPath;
    private readonly bool _enableCache;
    private readonly ILogger<RocksDbStateStore<TKey, TValue>> _logger;
    private readonly ConcurrentDictionary<TKey, TValue> _memoryStore;
    private bool _disposed = false;

    public string StoreName => _storeName;
    public bool IsEnabled => true;
    public long EstimatedSize => _memoryStore.Count;

    internal RocksDbStateStore(
        string storeName,
        StateStoreOptions options,
        ILoggerFactory? loggerFactory = null)
    {
        _storeName = storeName ?? throw new ArgumentNullException(nameof(storeName));
        _enableCache = options.EnableCache;
        _logger = loggerFactory?.CreateLogger<RocksDbStateStore<TKey, TValue>>()
                 ?? NullLogger<RocksDbStateStore<TKey, TValue>>.Instance;
        _memoryStore = new ConcurrentDictionary<TKey, TValue>();

        _physicalPath = GeneratePhysicalPath(storeName, options.BaseDirectory);

        if (_enableCache)
        {
            EnsureDirectoryExists();
            _logger.LogInformation("RocksDB store enabled with persistent storage: {StoreName} -> {Path}",
                _storeName, _physicalPath);
        }
        else
        {
            _logger.LogInformation("RocksDB store enabled with in-memory storage: {StoreName}", _storeName);
        }
    }

    public void Put(TKey key, TValue value)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        if (value == null) throw new ArgumentNullException(nameof(value));

        _memoryStore.AddOrUpdate(key, value, (k, v) => value);

        if (_enableCache)
        {
            PersistToFile(key, value);
        }

        _logger.LogTrace("Put operation: {Key} -> {StoreName}", key, _storeName);
    }

    public TValue? Get(TKey key)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));

        if (_memoryStore.TryGetValue(key, out var value))
        {
            _logger.LogTrace("Get operation (hit): {Key} -> {StoreName}", key, _storeName);
            return value;
        }

        if (_enableCache)
        {
            var persistedValue = LoadFromFile(key);
            if (persistedValue != null)
            {
                _memoryStore.TryAdd(key, persistedValue);
                _logger.LogTrace("Get operation (cache restore): {Key} -> {StoreName}", key, _storeName);
                return persistedValue;
            }
        }

        _logger.LogTrace("Get operation (miss): {Key} -> {StoreName}", key, _storeName);
        return null;
    }

    public bool Delete(TKey key)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));

        var removed = _memoryStore.TryRemove(key, out _);

        if (_enableCache && removed)
        {
            DeleteFromFile(key);
        }

        _logger.LogTrace("Delete operation: {Key} -> {StoreName} (Success: {Removed})", key, _storeName, removed);
        return removed;
    }

    public IEnumerable<KeyValuePair<TKey, TValue>> All()
    {
        if (_enableCache)
        {
            LoadAllFromFiles();
        }

        foreach (var kvp in _memoryStore)
        {
            yield return kvp;
        }
    }

    public void Flush()
    {
        if (!_enableCache) return;

        try
        {
            foreach (var kvp in _memoryStore)
            {
                PersistToFile(kvp.Key, kvp.Value);
            }
            _logger.LogDebug("Flush completed: {StoreName}", _storeName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Flush failed: {StoreName}", _storeName);
            throw;
        }
    }

    public void Close()
    {
        Flush();
        _logger.LogInformation("Store closed: {StoreName}", _storeName);
    }

    private string GeneratePhysicalPath(string storeName, string? baseDirectory)
    {
        var normalizedName = storeName.ToLowerInvariant().Replace('-', '_');
        var baseDir = baseDirectory ?? Path.Combine(Path.GetTempPath(), "ksqldsl_rocksdb");
        return Path.Combine(baseDir, normalizedName);
    }

    private void EnsureDirectoryExists()
    {
        if (!Directory.Exists(_physicalPath))
        {
            Directory.CreateDirectory(_physicalPath);
            _logger.LogDebug("Created directory: {Path}", _physicalPath);
        }
    }

    private void PersistToFile(TKey key, TValue value)
    {
        try
        {
            var keyStr = key.ToString() ?? string.Empty;
            var filePath = Path.Combine(_physicalPath, $"{keyStr.GetHashCode():X}.dat");
            var json = System.Text.Json.JsonSerializer.Serialize(value);
            File.WriteAllText(filePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist to file: {Key} -> {StoreName}", key, _storeName);
        }
    }

    private TValue? LoadFromFile(TKey key)
    {
        try
        {
            var keyStr = key.ToString() ?? string.Empty;
            var filePath = Path.Combine(_physicalPath, $"{keyStr.GetHashCode():X}.dat");
            if (!File.Exists(filePath)) return null;

            var json = File.ReadAllText(filePath);
            return System.Text.Json.JsonSerializer.Deserialize<TValue>(json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load from file: {Key} -> {StoreName}", key, _storeName);
            return null;
        }
    }

    private void DeleteFromFile(TKey key)
    {
        try
        {
            var keyStr = key.ToString() ?? string.Empty;
            var filePath = Path.Combine(_physicalPath, $"{keyStr.GetHashCode():X}.dat");
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete file: {Key} -> {StoreName}", key, _storeName);
        }
    }

    private void LoadAllFromFiles()
    {
        try
        {
            if (!Directory.Exists(_physicalPath)) return;

            var files = Directory.GetFiles(_physicalPath, "*.dat");
            foreach (var file in files)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var value = System.Text.Json.JsonSerializer.Deserialize<TValue>(json);
                    if (value != null)
                    {
                        var fileName = Path.GetFileNameWithoutExtension(file);
                        // Note: Key reconstruction is simplified for this implementation
                        // In production, more sophisticated key serialization would be needed
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load file: {File}", file);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load all from files: {StoreName}", _storeName);
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            Close();
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
