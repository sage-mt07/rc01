using Kafka.Ksql.Linq.Configuration;
using System;

namespace Kafka.Ksql.Linq.Core.Configuration;

internal class CoreSettings
{
    public ValidationMode ValidationMode { get; set; } = ValidationMode.Strict;

    public string? KafkaBootstrapServers { get; set; }

    public string? ApplicationId { get; set; }

    public string? StateStoreDirectory { get; set; }

    public CoreSettings Clone()
    {
        return new CoreSettings
        {
            ValidationMode = ValidationMode,
            KafkaBootstrapServers = KafkaBootstrapServers,
            ApplicationId = ApplicationId,
            StateStoreDirectory = StateStoreDirectory
        };
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(KafkaBootstrapServers))
            throw new InvalidOperationException("KafkaBootstrapServers is required");

        if (string.IsNullOrWhiteSpace(ApplicationId))
            throw new InvalidOperationException("ApplicationId is required");

        if (string.IsNullOrWhiteSpace(StateStoreDirectory))
            throw new InvalidOperationException("StateStoreDirectory is required");
    }

}
