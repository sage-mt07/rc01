namespace Kafka.Ksql.Linq.Serialization.Avro.Core;

using Kafka.Ksql.Linq.Serialization.Avro.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;

/// <summary>
/// Avroデシリアライザーの実装
/// </summary>
public class AvroDeserializer<T> : IAvroDeserializer<T> where T : class
{
    private readonly ILogger<AvroDeserializer<T>>? _logger;
    private bool _disposed = false;

    public AvroDeserializer(ILoggerFactory? loggerFactory = null)
    {
        _logger = loggerFactory?.CreateLogger<AvroDeserializer<T>>()
      ?? NullLogger<AvroDeserializer<T>>.Instance;
    }

    public T Deserialize(ReadOnlySpan<byte> data)
    {
        throw new NotSupportedException("AvroDeserializer removed in Phase1");
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            // リソース解放処理
            _disposed = true;
        }
    }
}