using Kafka.Ksql.Linq;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Entities.Samples.Models;
using Kafka.Ksql.Linq.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

public class KsqlDslReceiverContext : KsqlContext
{
    public KsqlDslReceiverContext() : base(new Kafka.Ksql.Linq.Configuration.KsqlDslOptions()) { }
    protected override void OnModelCreating(IModelBuilder modelBuilder)
    {
        // register the dependent table as read-only
        modelBuilder.Entity<OrderSummary>(readOnly: true)
            .WithTopic("ordersum")
            .HasKey(s => s.ProductId);
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
            .BuildContext<KsqlDslReceiverContext>();

        await context.Set<OrderSummary>().ForEachAsync(summary =>
        {
            Console.WriteLine($"ordersum => Product {summary.ProductId} : {summary.TotalQuantity}");
            return Task.CompletedTask;
        });
    }
}
