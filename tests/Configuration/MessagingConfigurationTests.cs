using Kafka.Ksql.Linq.Messaging.Configuration;
using Kafka.Ksql.Linq.Core.Configuration;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Configuration;

public class MessagingConfigurationTests
{
    [Fact]
    public void CommonSection_PropertyGetters()
    {
        var section = new CommonSection
        {
            SaslMechanism = Confluent.Kafka.SaslMechanism.Plain,
            SaslUsername = "u",
            SaslPassword = "p",
            SslCaLocation = "ca",
            SslCertificateLocation = "cert",
            SslKeyLocation = "key",
            SslKeyPassword = "kp"
        };
        Assert.Equal(Confluent.Kafka.SaslMechanism.Plain, section.SaslMechanism);
        Assert.Equal("u", section.SaslUsername);
        Assert.Equal("p", section.SaslPassword);
        Assert.Equal("ca", section.SslCaLocation);
        Assert.Equal("cert", section.SslCertificateLocation);
        Assert.Equal("key", section.SslKeyLocation);
        Assert.Equal("kp", section.SslKeyPassword);
    }

    [Fact]
    public void ConsumerSection_PropertyGetter()
    {
        var sec = new ConsumerSection { PartitionAssignmentStrategy = "range" };
        Assert.Equal("range", sec.PartitionAssignmentStrategy);
    }

    [Fact]
    public void SchemaRegistrySection_PropertyGetters()
    {
        var sec = new SchemaRegistrySection
        {
            BasicAuthUserInfo = "info",
            BasicAuthCredentialsSource = BasicAuthCredentialsSource.SaslInherit,
            SslCaLocation = "ca",
            SslKeyPassword = "kp",
            SslKeystoreLocation = "store",
            SslKeystorePassword = "sp"
        };
        Assert.Equal("info", sec.BasicAuthUserInfo);
        Assert.Equal(BasicAuthCredentialsSource.SaslInherit, sec.BasicAuthCredentialsSource);
        Assert.Equal("ca", sec.SslCaLocation);
        Assert.Equal("kp", sec.SslKeyPassword);
        Assert.Equal("store", sec.SslKeystoreLocation);
        Assert.Equal("sp", sec.SslKeystorePassword);
    }

    [Fact]
    public void TopicCreationSection_PropertyGetters()
    {
        var sec = new TopicCreationSection
        {
            NumPartitions = 2,
            ReplicationFactor = 3,
            EnableAutoCreation = true
        };
        sec.Configs["retention.ms"] = "1000";
        Assert.Equal(2, sec.NumPartitions);
        Assert.Equal(3, sec.ReplicationFactor);
        Assert.True(sec.EnableAutoCreation);
        Assert.Equal("1000", sec.Configs["retention.ms"]);
    }

    [Fact]
    public void TopicSection_PropertyGetters()
    {
        var sec = new TopicSection { TopicName = "t", Creation = new TopicCreationSection() };
        Assert.Equal("t", sec.TopicName);
        Assert.NotNull(sec.Creation);
    }
}
