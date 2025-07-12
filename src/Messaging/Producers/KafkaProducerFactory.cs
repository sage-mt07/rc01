using System.Threading.Tasks;
using Kafka.Ksql.Linq.Messaging.Abstractions;

namespace Kafka.Ksql.Linq.Messaging.Producers;

internal class KafkaProducerFactory : IKafkaProducerFactory
{
    private readonly KafkaProducerManager _manager;

    public KafkaProducerFactory(KafkaProducerManager manager)
    {
        _manager = manager;
    }

    public Task<IKafkaProducer<T>> CreateAsync<T>() where T : class
    {
        return _manager.GetProducerAsync<T>();
    }
}
