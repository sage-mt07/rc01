using Confluent.Kafka;
namespace Kafka.Ksql.Linq.Messaging.Producers.Exception;
/// <summary>
/// バッチ内個別エラー
/// </summary>
public class BatchDeliveryError
{
    public int MessageIndex { get; set; }
    public Error Error { get; set; } = default!;
    public object? OriginalMessage { get; set; }
}




