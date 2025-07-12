using Confluent.SchemaRegistry;
using Kafka.Ksql.Linq.Configuration;
using Kafka.Ksql.Linq.Messaging.Configuration;
using Kafka.Ksql.Linq.Messaging.Producers;
using System.Collections.Generic;
using Microsoft.Extensions.Options;
using Xunit;
using static Kafka.Ksql.Linq.Tests.PrivateAccessor;

namespace Kafka.Ksql.Linq.Tests.Messaging;

public class KafkaProducerManagerExtraTests
{
    [Fact]
    public void CreateSchemaRegistryClient_UsesOptions()
    {
        var options = new KsqlDslOptions
        {
            SchemaRegistry = new SchemaRegistrySection
            {
                Url = "u",
                MaxCachedSchemas = 5,
                RequestTimeoutMs = 10,
                AdditionalProperties = new System.Collections.Generic.Dictionary<string,string>{{"p","v"}},
                SslKeyPassword = "pw"
            }
        };
        var manager = new KafkaProducerManager(Options.Create(options), null);
        var client = InvokePrivate<object>(manager, "CreateSchemaRegistryClient", System.Type.EmptyTypes);
        Assert.Equal("CachedSchemaRegistryClient", client!.GetType().Name);
    }

    [Fact]
    public void BuildProducerConfig_WithSecurityAndPartitioner()
    {
        var options = new KsqlDslOptions
        {
            Common = new CommonSection
            {
                BootstrapServers = "s",
                ClientId = "c",
                SecurityProtocol = Confluent.Kafka.SecurityProtocol.SaslSsl,
                SaslMechanism = Confluent.Kafka.SaslMechanism.Plain,
                SaslUsername = "u",
                SaslPassword = "p",
                SslCaLocation = "ca",
                SslCertificateLocation = "cert",
                SslKeyLocation = "key",
                SslKeyPassword = "kp"
            },
            Topics = new Dictionary<string, TopicSection>
            {
                ["t"] = new TopicSection
                {
                    Producer = new ProducerSection
                    {
                        Acks = "All",
                        CompressionType = "Gzip",
                        Partitioner = "m"
                    }
                }
            }
        };

        var manager = new KafkaProducerManager(Options.Create(options), new Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory());
        var config = InvokePrivate<Confluent.Kafka.ProducerConfig>(manager, "BuildProducerConfig", new[] { typeof(string) }, null, "t");

        Assert.Equal(Confluent.Kafka.SecurityProtocol.SaslSsl, config.SecurityProtocol);
        Assert.Equal(Confluent.Kafka.SaslMechanism.Plain, config.SaslMechanism);
        Assert.Equal("u", config.SaslUsername);
        Assert.Equal("p", config.SaslPassword);
        Assert.Equal("ca", config.SslCaLocation);
        Assert.Equal("cert", config.SslCertificateLocation);
        Assert.Equal("key", config.SslKeyLocation);
        Assert.Equal("kp", config.SslKeyPassword);
        var entries = (System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string, string>>)config;
        Assert.Contains(entries, kv => kv.Key == "partitioner.class" && kv.Value == "m");
    }

    [Fact]
    public void CreateSchemaRegistryClient_WithAuthAndSsl()
    {
        var options = new KsqlDslOptions
        {
            SchemaRegistry = new SchemaRegistrySection
            {
                Url = "url",
                MaxCachedSchemas = 1,
                RequestTimeoutMs = 5,
                BasicAuthUserInfo = "x:y",
                BasicAuthCredentialsSource = BasicAuthCredentialsSource.UserInfo,
                SslCaLocation = "ca",
                SslKeystoreLocation = "ks",
                SslKeystorePassword = "pw",
                SslKeyPassword = "kp"
            }
        };

        var manager = new KafkaProducerManager(Options.Create(options), null);
        var client = InvokePrivate<object>(manager, "CreateSchemaRegistryClient", System.Type.EmptyTypes);
        Assert.NotNull(client);
    }
}
