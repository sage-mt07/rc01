using Confluent.Kafka;
using System.Collections.Generic;

namespace Kafka.Ksql.Linq.Messaging.Configuration;
public class CommonSection
{
    /// <summary>
    /// Kafka broker addresses (comma separated)
    /// </summary>
    public string BootstrapServers { get; init; } = "localhost:9092";

    /// <summary>
    /// Client ID
    /// </summary>
    public string ClientId { get; init; } = "ksql-dsl-client";

    /// <summary>
    /// Request timeout (ms)
    /// </summary>
    public int RequestTimeoutMs { get; init; } = 30000;

    /// <summary>
    /// Metadata max age (ms)
    /// </summary>
    public int MetadataMaxAgeMs { get; init; } = 300000;

    /// <summary>
    /// Security protocol
    /// </summary>
    public SecurityProtocol SecurityProtocol { get; init; } = SecurityProtocol.Plaintext;

    /// <summary>
    /// SASL mechanism
    /// </summary>
    public SaslMechanism? SaslMechanism { get; init; }

    /// <summary>
    /// SASL user name
    /// </summary>
    public string? SaslUsername { get; init; }

    /// <summary>
    /// SASL password
    /// </summary>
    public string? SaslPassword { get; init; }

    /// <summary>
    /// SSL CA certificate location
    /// </summary>
    public string? SslCaLocation { get; init; }

    /// <summary>
    /// SSL certificate location
    /// </summary>
    public string? SslCertificateLocation { get; init; }

    /// <summary>
    /// SSL key location
    /// </summary>
    public string? SslKeyLocation { get; init; }

    /// <summary>
    /// SSL key password
    /// </summary>
    public string? SslKeyPassword { get; init; }

    /// <summary>
    /// Additional configuration properties
    /// </summary>
    public Dictionary<string, string> AdditionalProperties { get; init; } = new();
}
