namespace Kafka.Ksql.Linq.Mapping;

using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

public class MappingManager : IMappingManager
{
    private readonly Dictionary<Type, EntityModel> _models = new();

    public void Register<TEntity>(EntityModel model) where TEntity : class
    {
        if (model == null) throw new ArgumentNullException(nameof(model));
        _models[typeof(TEntity)] = model;
    }

    public (object Key, TEntity Value) ExtractKeyValue<TEntity>(TEntity entity) where TEntity : class
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        if (!_models.TryGetValue(typeof(TEntity), out var model))
            throw new InvalidOperationException($"Model for {typeof(TEntity).Name} is not registered.");

        try
        {
            var key = KeyExtractor.ExtractKeyValue(entity, model);

            if (key != null && key is not Dictionary<string, object> && !KeyExtractor.IsSupportedKeyType(key.GetType()))
            {
                throw new NotSupportedException($"Key type {key.GetType().Name} is not supported.");
            }

            return (key!, entity);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidCastException or FormatException)
        {
            throw new InvalidOperationException($"Failed to extract key for {typeof(TEntity).Name}: {ex.Message}", ex);
        }
    }
}
