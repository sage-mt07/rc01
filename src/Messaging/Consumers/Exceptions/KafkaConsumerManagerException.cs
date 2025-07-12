using Kafka.Ksql.Linq.Core.Exceptions;
using System;

namespace Kafka.Ksql.Linq.Messaging.Consumers.Exceptions;

/// <summary>
/// Consumer管理例外
/// </summary>
public class KafkaConsumerManagerException : KafkaMessageBusException
{
    public KafkaConsumerManagerException(string message) : base(message) { }
    public KafkaConsumerManagerException(string message, Exception innerException) : base(message, innerException) { }
}
