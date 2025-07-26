using System.Collections.Generic;

namespace Kafka.Ksql.Linq.Query.Ddl;

public class DdlSchemaBuilder
{
    private readonly string _objectName;
    private readonly DdlObjectType _objectType;
    private string _topicName = string.Empty;
    private int _partitions = 1;
    private short _replicas = 1;
    private readonly List<ColumnDefinition> _columns = new();

    public DdlSchemaBuilder(string objectName, DdlObjectType objectType)
    {
        _objectName = objectName;
        _objectType = objectType;
    }

    public DdlSchemaBuilder WithTopic(string topic)
    {
        _topicName = topic;
        return this;
    }

    public DdlSchemaBuilder WithPartitions(int partitions)
    {
        _partitions = partitions;
        return this;
    }

    public DdlSchemaBuilder WithReplicas(short replicas)
    {
        _replicas = replicas;
        return this;
    }

    public DdlSchemaBuilder AddColumn(string name, string type, bool isKey = false)
    {
        _columns.Add(new ColumnDefinition(name, type, isKey));
        return this;
    }

    public DdlSchemaDefinition Build()
    {
        return new DdlSchemaDefinition(
            _objectName,
            _topicName,
            _objectType,
            _partitions,
            _replicas,
            _columns);
    }
}
