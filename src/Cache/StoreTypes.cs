namespace Kafka.Ksql.Linq.Cache;

/// <summary>
/// Known state store type names.
/// </summary>
internal static class StoreTypes
{
    /// <summary>
    /// RocksDB-backed store implementation.
    /// </summary>
    public const string RocksDb = "RocksDb";

    /// <summary>
    /// Streamiz.Kafka.Net RocksDB-backed implementation.
    /// </summary>
    public const string StreamizRocksDb = "StreamizRocksDb";
}

