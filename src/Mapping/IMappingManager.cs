namespace Kafka.Ksql.Linq.Mapping;

using Kafka.Ksql.Linq.Core.Abstractions;

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
}
