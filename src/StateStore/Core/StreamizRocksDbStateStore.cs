using Kafka.Ksql.Linq.StateStore.Configuration;
using Microsoft.Extensions.Logging;
using Streamiz.Kafka.Net.State.RocksDb;
using Streamiz.Kafka.Net.Crosscutting;
using Streamiz.Kafka.Net;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Kafka.Ksql.Linq.StateStore.Core;

/// <summary>
/// IStateStore implementation backed by Streamiz.Kafka.Net RocksDbKeyValueStore.
/// This provides persistence via RocksDB using the Streamiz library.
/// </summary>
internal class StreamizRocksDbStateStore<TKey, TValue> : IStateStore<TKey, TValue>
    where TKey : notnull
    where TValue : class
{
    private readonly RocksDbKeyValueStore _store;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger<StreamizRocksDbStateStore<TKey, TValue>> _logger;
    private bool _disposed;

    public string StoreName => _store.Name;
    public bool IsEnabled => true;
    public long EstimatedSize => _store.ApproximateNumEntries();

    internal StreamizRocksDbStateStore(
        string storeName,
        StateStoreOptions options,
        ILoggerFactory? loggerFactory = null)
    {
        if (string.IsNullOrWhiteSpace(storeName))
            throw new ArgumentNullException(nameof(storeName));

        _logger = loggerFactory?.CreateLogger<StreamizRocksDbStateStore<TKey, TValue>>()
            ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<StreamizRocksDbStateStore<TKey, TValue>>.Instance;
        _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        var dir = options.BaseDirectory ?? System.IO.Path.Combine(System.IO.Path.GetTempPath(), "streamiz_stores");
        _store = new RocksDbKeyValueStore(storeName, dir);
        try
        {
            var context = (ProcessorContext)Activator.CreateInstance(typeof(ProcessorContext), nonPublic: true)!;
            _store.Init(context, _store);
            _logger.LogDebug("Initialized Streamiz RocksDb store {StoreName} at {Dir}", storeName, dir);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize Streamiz store {StoreName}", storeName);
        }
    }

    private Bytes SerializeKey(TKey key)
    {
#pragma warning disable CS0618
        return new Bytes(System.Text.Encoding.UTF8.GetBytes(key.ToString()!));
#pragma warning restore CS0618
    }

    private byte[] SerializeValue(TValue value)
        => JsonSerializer.SerializeToUtf8Bytes(value, _jsonOptions);

    private TValue? DeserializeValue(byte[]? data)
    {
        if (data == null) return null;
        try
        {
            return JsonSerializer.Deserialize<TValue>(data, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize value from store {StoreName}", StoreName);
            return null;
        }
    }

    public void Put(TKey key, TValue value)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(StreamizRocksDbStateStore<TKey, TValue>));
        _store.Put(SerializeKey(key), SerializeValue(value));
    }

    public TValue? Get(TKey key)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(StreamizRocksDbStateStore<TKey, TValue>));
        var bytes = _store.Get(SerializeKey(key));
        return DeserializeValue(bytes);
    }

    public bool Delete(TKey key)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(StreamizRocksDbStateStore<TKey, TValue>));
        var old = _store.Delete(SerializeKey(key));
        return old != null;
    }

    public IEnumerable<KeyValuePair<TKey, TValue>> All()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(StreamizRocksDbStateStore<TKey, TValue>));
        foreach (var kv in _store.All())
        {
            var value = DeserializeValue(kv.Value);
            if (value != null)
            {
                var keyStr = System.Text.Encoding.UTF8.GetString(kv.Key.Get);
                if (typeof(TKey) == typeof(string))
                {
                    yield return new KeyValuePair<TKey, TValue>((TKey)(object)keyStr, value);
                }
            }
        }
    }

    public void Flush()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(StreamizRocksDbStateStore<TKey, TValue>));
        _store.Flush();
    }

    public void Close()
    {
        _store.Close();
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
