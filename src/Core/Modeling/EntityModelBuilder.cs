using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Query.Abstractions;
using System;
using System.Linq;
using System.Reflection;

namespace Kafka.Ksql.Linq.Core.Modeling;

public class EntityModelBuilder<T> : IEntityBuilder<T> where T : class
{
    private readonly EntityModel _entityModel;

    internal EntityModelBuilder(EntityModel entityModel)
    {
        _entityModel = entityModel ?? throw new ArgumentNullException(nameof(entityModel));
    }
    public IEntityBuilder<T> AsTable(string? topicName = null, bool useCache = true)
    {
        _entityModel.SetStreamTableType(StreamTableType.Table);
        _entityModel.EnableCache = useCache;
        if (!string.IsNullOrWhiteSpace(topicName))
        {
            _entityModel.TopicName = topicName.ToLowerInvariant();
        }
        return this;
    }
    public IEntityBuilder<T> AsStream()
    {
        _entityModel.SetStreamTableType(StreamTableType.Stream);
        return this;
    }
    public IEntityBuilder<T> WithManualCommit()
    {
        _entityModel.UseManualCommit = true;
        return this;
    }

    public EntityModelBuilder<T> OnError(ErrorAction action)
    {
        _entityModel.ErrorAction = action;
        return this;
    }


    public EntityModelBuilder<T> WithPartitions(int partitions)
    {
        if (partitions <= 0)
            throw new ArgumentException("Partitions must be greater than 0", nameof(partitions));

        _entityModel.DecimalPrecisions.Clear(); // no partition info now
        // partitions info removed in new API
        return this;
    }

    public EntityModelBuilder<T> WithReplicationFactor(int replicationFactor)
    {
        if (replicationFactor <= 0)
            throw new ArgumentException("ReplicationFactor must be greater than 0", nameof(replicationFactor));

        // replication info removed in new API
        return this;
    }


    public EntityModelBuilder<T> WithPartitioner(string partitioner)
    {
        if (string.IsNullOrWhiteSpace(partitioner))
            throw new ArgumentException("Partitioner cannot be null or empty", nameof(partitioner));

        // partitioner info removed in new API
        return this;
    }

    public IEntityBuilder<T> WithTopic(string name, int partitions = 1, int replication = 1)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Topic name cannot be null or empty", nameof(name));

        _entityModel.TopicName = name.ToLowerInvariant();
        return this;
    }

    public EntityModel GetModel()
    {
        return _entityModel;
    }

    [Obsolete("Changing the topic name via Fluent API is prohibited in POCO attribute-driven mode. Use the [Topic] attribute instead.", true)]
    public EntityModelBuilder<T> HasTopicName(string topicName)
    {
        throw new NotSupportedException("Changing the topic name via Fluent API is prohibited in POCO attribute-driven mode. Use the [Topic] attribute instead.");
    }

    /// <summary>
    /// Configures the primary key for the entity using a LINQ expression.
    /// </summary>
    /// <typeparam name="TKey">Type of the key property.</typeparam>
    /// <param name="keyExpression">Expression selecting the key property or composite key.</param>
    /// <returns>The same <see cref="EntityModelBuilder{T}"/> instance for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="keyExpression"/> is <c>null</c>.</exception>
    public IEntityBuilder<T> HasKey<TKey>(System.Linq.Expressions.Expression<Func<T, TKey>> keyExpression)
    {
        if (keyExpression == null)
            throw new ArgumentNullException(nameof(keyExpression));

        var properties = ExtractProperties(keyExpression);
        _entityModel.KeyProperties = properties;
        return this;
    }

    public IEntityBuilder<T> WithDecimalPrecision<TProp>(System.Linq.Expressions.Expression<Func<T, TProp>> property, int precision, int scale)
    {
        if (property.Body is System.Linq.Expressions.MemberExpression member && member.Member is PropertyInfo prop)
        {
            _entityModel.DecimalPrecisions[prop] = (precision, scale);
            return this;
        }
        throw new ArgumentException("Invalid property expression", nameof(property));
    }


    private static PropertyInfo[] ExtractProperties<TKey>(System.Linq.Expressions.Expression<Func<T, TKey>> expression)
    {
        if (expression.Body is System.Linq.Expressions.MemberExpression memberExpr && memberExpr.Member is PropertyInfo prop)
        {
            return new[] { prop };
        }

        if (expression.Body is System.Linq.Expressions.NewExpression newExpr)
        {
            return newExpr.Arguments
                .OfType<System.Linq.Expressions.MemberExpression>()
                .Select(m => m.Member)
                .OfType<PropertyInfo>()
                .ToArray();
        }

        throw new ArgumentException("Invalid key expression", nameof(expression));
    }

    public override string ToString()
    {
        var entityName = _entityModel.EntityType.Name;
        var topicName = _entityModel.TopicName ?? "undefined";
        var keyCount = _entityModel.KeyProperties.Length;
        var validStatus = _entityModel.IsValid ? "valid" : "invalid";

        return $"Entity: {entityName}, Topic: {topicName}, Keys: {keyCount}, Status: {validStatus}";
    }

}
