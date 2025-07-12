using System.Collections.Generic;

namespace Kafka.Ksql.Linq.StateStore.Configuration;

internal class StateStoreConfiguration
{
    public string StoreType { get; set; } = StoreTypes.RocksDb;
    public string? StoreName { get; set; }
    public bool EnableCache { get; set; } = false;
    public List<int> Windows { get; set; } = new();
    public string? BaseDirectory { get; set; }

    public StateStoreOptions ToStateStoreOptions()
    {
        return new StateStoreOptions
        {
            StoreType = StoreType,
            StoreName = StoreName,
            EnableCache = EnableCache,
            Windows = Windows,
            BaseDirectory = BaseDirectory
        };
    }
}
