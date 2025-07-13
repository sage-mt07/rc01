namespace Kafka.Ksql.Linq.Mapping;

using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Core.Models;
using System.Collections.Generic;

public interface IMappingManager
{
    /// <summary>
    /// Registers an entity model for mapping.
    /// </summary>
    void Register<TEntity>(EntityModel model) where TEntity : class;

    /// <summary>
    /// Extracts key/value pair from the entity according to its model.
    /// </summary>
    (object Key, TEntity Value) ExtractKeyValue<TEntity>(TEntity entity) where TEntity : class;

    /// <summary>
    /// Extracts composite key parts from the entity (new recommended API).
    /// </summary>
    List<CompositeKeyPart> ExtractKeyParts<TEntity>(TEntity entity) where TEntity : class;
}
