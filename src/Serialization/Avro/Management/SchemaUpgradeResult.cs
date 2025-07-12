using System;
using Kafka.Ksql.Linq.Serialization.Avro.Exceptions;

namespace Kafka.Ksql.Linq.Serialization.Avro.Management;
public class SchemaUpgradeResult
{
    public bool Success { get; set; }
    public int? NewSchemaId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public SchemaRegistrationFailureCategory? FailureCategory { get; set; }
    public DateTime UpgradedAt { get; set; } = DateTime.UtcNow;
}
