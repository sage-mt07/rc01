using Kafka.Ksql.Linq.Serialization.Avro.Core;
using System;
using System.Collections.Generic;

namespace Kafka.Ksql.Linq.Serialization.Avro.Management;
internal interface IAvroSchemaRepository
{
    void StoreSchemaInfo(AvroSchemaInfo schemaInfo);
    AvroSchemaInfo? GetSchemaInfo(Type entityType);
    AvroSchemaInfo? GetSchemaInfoByTopic(string topicName);
    List<AvroSchemaInfo> GetAllSchemas();
    bool IsRegistered(Type entityType);
    void Clear();
}
