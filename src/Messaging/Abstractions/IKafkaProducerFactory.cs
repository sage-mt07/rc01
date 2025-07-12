namespace Kafka.Ksql.Linq.Messaging.Abstractions;

using System.Threading.Tasks;

public interface IKafkaProducerFactory
{
    Task<IKafkaProducer<T>> CreateAsync<T>() where T : class;
}
