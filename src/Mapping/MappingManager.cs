namespace Kafka.Ksql.Linq.Mapping;

using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Core.Models;
using System;
using System.Collections.Generic;

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
        if (entity == null) throw new ArgumentNullException(nameof(entity));
        if (!_models.TryGetValue(typeof(TEntity), out var model))
            throw new InvalidOperationException($"Model for {typeof(TEntity).Name} is not registered.");

        var key = KeyExtractor.ExtractKeyValue(entity, model);
        return (key, entity);
    }
}
