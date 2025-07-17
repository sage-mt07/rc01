namespace Kafka.Ksql.Linq.Cache.Core;

internal interface ITableCache<T> : System.IDisposable where T : class
{
    bool IsRunning { get; }

    System.Threading.Tasks.Task InitializeAsync();

    bool TryGet(string key, out T? value);

    System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string, T>> GetAll();
}
