using Kafka.Ksql.Linq.Serialization.Avro.Core;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kafka.Ksql.Linq.Serialization.Abstractions;
internal interface ISchemaRegistryClient : IDisposable
{
    Task<(int keySchemaId, int valueSchemaId)> RegisterTopicSchemasAsync(string topicName, string keySchema, string valueSchema);

    Task<int> RegisterKeySchemaAsync(string topicName, string keySchema);

    Task<int> RegisterValueSchemaAsync(string topicName, string valueSchema);

    Task<int> RegisterSchemaAsync(string subject, string avroSchema);

    Task<AvroSchemaInfo> GetLatestSchemaAsync(string subject);

    Task<AvroSchemaInfo> GetSchemaByIdAsync(int schemaId);

    Task<bool> CheckCompatibilityAsync(string subject, string avroSchema);

    Task<IList<int>> GetSchemaVersionsAsync(string subject);

    Task<AvroSchemaInfo> GetSchemaAsync(string subject, int version);

    Task<IList<string>> GetAllSubjectsAsync();
}

