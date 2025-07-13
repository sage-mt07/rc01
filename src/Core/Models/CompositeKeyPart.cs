using System;

namespace Kafka.Ksql.Linq.Core.Models;

public readonly struct CompositeKeyPart
{
    public string KeyName { get; }
    public Type KeyType { get; }
    public string Value { get; }

    public CompositeKeyPart(string keyName, Type keyType, string value)
    {
        KeyName = keyName;
        KeyType = keyType;
        Value = value;
    }
}
