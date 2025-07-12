using System;
using System.Linq.Expressions;

namespace Kafka.Ksql.Linq.Core.Abstractions;

public interface IEntityBuilder<T> where T : class
{
    IEntityBuilder<T> WithTopic(string name, int partitions = 1, int replication = 1);
    IEntityBuilder<T> HasKey<TKey>(Expression<Func<T, TKey>> key);
    IEntityBuilder<T> AsTable(string? topicName = null, bool useCache = true);
    IEntityBuilder<T> AsStream();
    IEntityBuilder<T> WithManualCommit();
    IEntityBuilder<T> WithDecimalPrecision<TProp>(Expression<Func<T, TProp>> property, int precision, int scale);
}
