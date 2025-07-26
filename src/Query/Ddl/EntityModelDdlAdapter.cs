using System.Linq;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Query.Pipeline;
using Kafka.Ksql.Linq.Query.Abstractions;
using Kafka.Ksql.Linq.Query.Schema;
using Kafka.Ksql.Linq.Query.Builders.Common;
using Kafka.Ksql.Linq.Core.Extensions;

namespace Kafka.Ksql.Linq.Query.Ddl;

public class EntityModelDdlAdapter : IDdlSchemaProvider
{
    private readonly EntityModel _model;

    public EntityModelDdlAdapter(EntityModel model)
    {
        _model = model;
    }

    public DdlSchemaDefinition GetSchema()
    {
        var builder = new DdlSchemaBuilder(
            _model.EntityType.Name.ToLowerInvariant(),
            _model.GetExplicitStreamTableType() == StreamTableType.Table ? DdlObjectType.Table : DdlObjectType.Stream)
            .WithTopic(_model.GetTopicName())
            .WithPartitions(_model.Partitions > 0 ? _model.Partitions : 1)
            .WithReplicas(_model.ReplicationFactor > 0 ? _model.ReplicationFactor : (short)1);

        foreach (var property in _model.EntityType.GetProperties().OrderBy(p => p.MetadataToken))
        {
            var columnName = KsqlNameUtils.Sanitize(property.Name);
            var type = Schema.KsqlTypeMapping.MapToKsqlType(property.PropertyType);
            builder.AddColumn(columnName, type, _model.KeyProperties.Contains(property));
        }

        return builder.Build();
    }
}
