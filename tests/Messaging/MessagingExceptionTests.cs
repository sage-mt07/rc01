using System;
using Kafka.Ksql.Linq.Messaging.Consumers.Exceptions;
using Kafka.Ksql.Linq.Messaging.Producers.Exception;
using Kafka.Ksql.Linq.Messaging.Producers.Core;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Messaging;

public class MessagingExceptionTests
{
    [Fact]
    public void KafkaConsumerException_Constructors()
    {
        var ex1 = new KafkaConsumerException("e1");
        Assert.Equal("e1", ex1.Message);
        var inner = new Exception("i");
        var ex2 = new KafkaConsumerException("e2", inner);
        Assert.Equal(inner, ex2.InnerException);
    }

    [Fact]
    public void KafkaBatchSendException_SetsBatchResult()
    {
        var batch = new KafkaBatchDeliveryResult { Topic = "t" };
        var ex = new KafkaBatchSendException("bad", batch);
        Assert.Equal(batch, ex.BatchResult);
    }

    [Fact]
    public void KafkaProducerManagerException_Constructors()
    {
        var ex1 = new KafkaProducerManagerException("m");
        Assert.Equal("m", ex1.Message);
        var inner = new Exception("i");
        var ex2 = new KafkaProducerManagerException("m2", inner);
        Assert.Equal(inner, ex2.InnerException);
    }

}
