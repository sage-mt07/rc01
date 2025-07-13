namespace Kafka.Ksql.Linq.Mapping;

using Kafka.Ksql.Linq.Core.Models;
using System;

/// <summary>
/// Holds generated key/value types and their associated PropertyMeta information.
/// </summary>
public class KeyValueTypeMapping
{
    public Type KeyType { get; set; } = default!;
    public PropertyMeta[] KeyProperties { get; set; } = Array.Empty<PropertyMeta>();
    public Type ValueType { get; set; } = default!;
    public PropertyMeta[] ValueProperties { get; set; } = Array.Empty<PropertyMeta>();
}
