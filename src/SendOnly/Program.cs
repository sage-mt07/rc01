using Kafka.Ksql.Linq;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Entities.Samples.Models;
using Kafka.Ksql.Linq.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using Kafka.Ksql.Linq.Core.Modeling;

public class KsqlDslSenderContext : KsqlContext
{
    public KsqlDslSenderContext() : base(new Kafka.Ksql.Linq.Configuration.KsqlDslOptions()) { }
    protected override void OnModelCreating(IModelBuilder modelBuilder)
    {
        // source entity for sending
        modelBuilder.Entity<Order>()
            .WithTopic("orders")
            .HasKey(o => o.OrderId);

        // define aggregation query that the receiver depends on
        modelBuilder.DefineQuery<Order, OrderSummary>(orders => orders
            .GroupBy(o => o.ProductId)
            .Select(g => new OrderSummary
            {
                ProductId = g.Key,
                TotalQuantity = g.Sum(o => o.Quantity)
            }))
            .AsTable("ordersum");
    }
}

class Program
{
    static async Task Main(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();

        var context = KsqlContextBuilder.Create()
            .UseConfiguration(configuration)
            .UseSchemaRegistry(configuration["KsqlDsl:SchemaRegistry:Url"]!)
            .EnableLogging(LoggerFactory.Create(builder => builder.AddConsole()))
            .BuildContext<KsqlDslSenderContext>();

        int id = 1;
        while (true)
        {
            var order = new Order
            {
                OrderId = id++,
                UserId = Random.Shared.Next(1, 5),
                ProductId = Random.Shared.Next(1, 3),
                Quantity = Random.Shared.Next(1, 10)
            };
            await context.Set<Order>().AddAsync(order);
            await Task.Delay(1000);
        }
    }
}
