using System;
using System.Collections.Generic;

namespace Kafka.Ksql.Linq.StateStore.Core;

internal interface IStateStore<TKey, TValue> : IDisposable
    where TKey : notnull
    where TValue : class
{
    string StoreName { get; }
    bool IsEnabled { get; }
    long EstimatedSize { get; }

    void Put(TKey key, TValue value);
    TValue? Get(TKey key);
    bool Delete(TKey key);
    IEnumerable<KeyValuePair<TKey, TValue>> All();
    void Flush();
    void Close();
}
