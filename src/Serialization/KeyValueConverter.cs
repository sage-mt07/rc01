namespace Kafka.Ksql.Linq.Serialization;

using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Core.Models;
using System;

/// <summary>
/// Helper for splitting an entity into key/value and merging them back.
/// </summary>
public static class KeyValueConverter
{
    public static (object Key, TEntity Value) Split<TEntity>(TEntity entity, EntityModel model) where TEntity : class
    {
        if (entity == null) throw new ArgumentNullException(nameof(entity));
        if (model == null) throw new ArgumentNullException(nameof(model));
        var parts = KeyExtractor.ExtractKeyParts(entity, model);
        var key = KeyExtractor.BuildTypedKey(parts);
        return (key!, entity);
    }

    public static TEntity Merge<TEntity>(object? key, TEntity valueEntity, EntityModel model) where TEntity : class
    {
        if (valueEntity == null) throw new ArgumentNullException(nameof(valueEntity));
        if (model == null) throw new ArgumentNullException(nameof(model));
        return KeyMerger.MergeKeyValue(key, valueEntity, model);
    }
}
