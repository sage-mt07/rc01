namespace Kafka.Ksql.Linq.Serialization.Avro.Core;

using Kafka.Ksql.Linq.Serialization.Avro.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.IO;

/// <summary>
/// Avroシリアライザーの実装
/// </summary>
public class AvroSerializer<T> : IAvroSerializer<T> where T : class
{
    private readonly ILogger<AvroSerializer<T>>? _logger;
    private bool _disposed = false;

    public AvroSerializer(ILoggerFactory? loggerFactory = null)
    {
        _logger = loggerFactory?.CreateLogger<AvroSerializer<T>>()
       ?? NullLogger<AvroSerializer<T>>.Instance;
    }

    public void Serialize(T value, Stream stream)
    {
        throw new NotSupportedException("AvroSerializer removed in Phase1");
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