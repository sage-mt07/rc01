using Kafka.Ksql.Linq.Messaging.Consumers.Exceptions;
using System;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Messaging;

public class KafkaConsumerManagerExceptionTests
{
    [Fact]
    public void Constructors_SetProperties()
    {
        var ex1 = new KafkaConsumerManagerException("msg");
        Assert.Equal("msg", ex1.Message);
        var inner = new Exception("inner");
        var ex2 = new KafkaConsumerManagerException("m", inner);
        Assert.Equal(inner, ex2.InnerException);
    }
}
