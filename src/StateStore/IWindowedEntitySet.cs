using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.StateStore.Core;

namespace Kafka.Ksql.Linq.StateStore;

internal interface IWindowedEntitySet<T> : IEntitySet<T> where T : class
{
    int WindowMinutes { get; }
    IStateStore<string, T> GetStateStore();
    string GetWindowTableName();
}
