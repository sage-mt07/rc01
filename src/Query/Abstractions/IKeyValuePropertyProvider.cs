namespace Kafka.Ksql.Linq.Query.Abstractions;

using System;
using Kafka.Ksql.Linq.Core.Models;

/// <summary>
/// Provides metadata for key and value properties of a POCO type.
/// </summary>
public interface IKeyValuePropertyProvider
{
    PropertyMeta[] GetKeyProperties(Type pocoType);
    PropertyMeta[] GetValueProperties(Type pocoType);
}
