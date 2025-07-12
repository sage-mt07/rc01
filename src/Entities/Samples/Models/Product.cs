namespace Kafka.Ksql.Linq.Entities.Samples.Models;

public class Product
{
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}
