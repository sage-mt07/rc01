using System;

namespace Kafka.Ksql.Linq.Serialization.Avro.Exceptions;
public class AvroSchemaRegistrationException : Exception
{
    public AvroSchemaRegistrationException(string message) : base(message) { }
    public AvroSchemaRegistrationException(string message, Exception innerException) : base(message, innerException) { }
}
