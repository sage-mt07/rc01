namespace Kafka.Ksql.Linq.Mapping;

using Kafka.Ksql.Linq.Core.Models;
using System;
using System.Reflection;

/// <summary>
/// Holds generated key/value types and their associated PropertyMeta information.
/// </summary>
public class KeyValueTypeMapping
{
    public Type KeyType { get; set; } = default!;
    public PropertyMeta[] KeyProperties { get; set; } = Array.Empty<PropertyMeta>();
    public PropertyInfo[] KeyTypeProperties { get; set; } = Array.Empty<PropertyInfo>();

    public Type ValueType { get; set; } = default!;
    public PropertyMeta[] ValueProperties { get; set; } = Array.Empty<PropertyMeta>();
    public PropertyInfo[] ValueTypeProperties { get; set; } = Array.Empty<PropertyInfo>();

    /// <summary>
    /// Extract key object from POCO instance based on registered PropertyMeta.
    /// </summary>
    public object ExtractKey(object poco)
    {
        if (poco == null) throw new ArgumentNullException(nameof(poco));
        var keyInstance = Activator.CreateInstance(KeyType)!;
        for (int i = 0; i < KeyProperties.Length; i++)
        {
            var meta = KeyProperties[i];
            var value = meta.PropertyInfo!.GetValue(poco);
            KeyTypeProperties[i].SetValue(keyInstance, value);
        }
        return keyInstance;
    }

    /// <summary>
    /// Extract value object from POCO instance based on registered PropertyMeta.
    /// </summary>
    public object ExtractValue(object poco)
    {
        if (poco == null) throw new ArgumentNullException(nameof(poco));
        var valueInstance = Activator.CreateInstance(ValueType)!;
        for (int i = 0; i < ValueProperties.Length; i++)
        {
            var meta = ValueProperties[i];
            var value = meta.PropertyInfo!.GetValue(poco);
            ValueTypeProperties[i].SetValue(valueInstance, value);
        }
        return valueInstance;
    }

    /// <summary>
    /// Combine key and value objects into a POCO instance of the specified type.
    /// </summary>
    public object CombineFromKeyValue(object? key, object value, Type pocoType)
    {
        if (value == null) throw new ArgumentNullException(nameof(value));
        if (pocoType == null) throw new ArgumentNullException(nameof(pocoType));

        var instance = Activator.CreateInstance(pocoType)!;

        // set value properties
        for (int i = 0; i < ValueProperties.Length; i++)
        {
            var meta = ValueProperties[i];
            var val = ValueTypeProperties[i].GetValue(value);
            meta.PropertyInfo!.SetValue(instance, val);
        }

        if (key != null)
        {
            for (int i = 0; i < KeyProperties.Length; i++)
            {
                var meta = KeyProperties[i];
                var val = KeyTypeProperties[i].GetValue(key);
                meta.PropertyInfo!.SetValue(instance, val);
            }
        }

        return instance;
    }
}
