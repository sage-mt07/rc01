using System.Threading.Tasks;
using Kafka.Ksql.Linq.Configuration.Abstractions;
using Kafka.Ksql.Linq.Messaging.Abstractions;

namespace Kafka.Ksql.Linq.Messaging.Consumers;

internal class KafkaConsumerFactory : IKafkaConsumerFactory
{
    private readonly KafkaConsumerManager _manager;

    public KafkaConsumerFactory(KafkaConsumerManager manager)
    {
        _manager = manager;
    }

    public Task<IKafkaConsumer<TValue, object>> CreateAsync<TValue>(KafkaSubscriptionOptions? options = null) where TValue : class
    {
        return _manager.GetConsumerAsync<TValue>(options);
    }
}
