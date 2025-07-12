namespace Kafka.Ksql.Linq.Serialization.Avro.Abstractions;

using System;


/// <summary>
/// Avroデシリアライザーのインターフェース
/// </summary>
public interface IAvroDeserializer<T> : IDisposable where T : class
{
    /// <summary>
    /// ReadOnlySpanからオブジェクトにデシリアライズ
    /// </summary>
    T Deserialize(ReadOnlySpan<byte> data);
}