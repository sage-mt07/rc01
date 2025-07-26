using System.Collections.Generic;

namespace Kafka.Ksql.Linq.Query.Ddl;

public record DdlSchemaDefinition(
    string ObjectName,
    string TopicName,
    DdlObjectType ObjectType,
    int Partitions,
    short Replicas,
    IReadOnlyList<ColumnDefinition> Columns);
