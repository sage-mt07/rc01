namespace Kafka.Ksql.Linq.Messaging.Abstractions;

using Kafka.Ksql.Linq.Configuration.Abstractions;
using System.Threading.Tasks;

public interface IKafkaConsumerFactory
{
    Task<IKafkaConsumer<TValue, object>> CreateAsync<TValue>(KafkaSubscriptionOptions? options = null) where TValue : class;
}
