using System.Threading.Tasks;

namespace Kafka.Ksql.Linq.Core.Abstractions;

public interface IManualCommitMessage<T> where T : class
{
    T Value { get; }
    Task CommitAsync();
    Task NegativeAckAsync();
}
