using Kafka.Ksql.Linq.Core.Exceptions;

namespace Kafka.Ksql.Linq.Messaging.Exceptions;

/// <summary>
/// Thrown when managed topic settings conflict with existing Kafka configuration.
/// </summary>
public class KafkaTopicConflictException : KafkaMessageBusException
{
    public KafkaTopicConflictException(string message) : base(message) { }
    public KafkaTopicConflictException(string message, System.Exception innerException) : base(message, innerException) { }
}
