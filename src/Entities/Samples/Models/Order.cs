namespace Kafka.Ksql.Linq.Entities.Samples.Models;

public class Order
{
    public int OrderId { get; set; }
    public int UserId { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
}
