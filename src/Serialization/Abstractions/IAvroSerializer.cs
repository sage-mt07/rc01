namespace Kafka.Ksql.Linq.Serialization.Avro.Abstractions;

using System;
using System.IO;

/// <summary>
/// Avroシリアライザーのインターフェース
/// </summary>
public interface IAvroSerializer<T> : IDisposable where T : class
{
    /// <summary>
    /// オブジェクトをストリームにシリアライズ
    /// </summary>
    void Serialize(T value, Stream stream);
}
