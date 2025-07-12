using System;
using System.Threading.Tasks;

namespace Kafka.Ksql.Linq.Window.Finalization;

public interface IKafkaProducer : IDisposable
{
    Task SendAsync(string topic, string key, object value);
}
