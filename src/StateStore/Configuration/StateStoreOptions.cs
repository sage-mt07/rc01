using System.Collections.Generic;

namespace Kafka.Ksql.Linq.StateStore.Configuration
{
    internal class StateStoreOptions
    {
        public string StoreType { get; set; } = StoreTypes.RocksDb;
        public string? StoreName { get; set; }
        public bool EnableCache { get; set; } = false;
        public List<int> Windows { get; set; } = new();
        public string? BaseDirectory { get; set; }
    }
}
