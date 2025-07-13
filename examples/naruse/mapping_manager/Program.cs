using Kafka.Ksql.Linq.Application;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Entities.Samples;
using Kafka.Ksql.Linq.Entities.Samples.Models;
using Kafka.Ksql.Linq.Mapping;
using Kafka.Ksql.Linq.Query.Analysis;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using System.Threading.Tasks;

public class SampleContext : KsqlContext
{
    protected override void OnModelCreating(IModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>()
            .WithTopic("orders")
            .HasKey(o => new { o.OrderId, o.UserId });
    }
}

class Program
{
    static async Task Main()
    {
        var services = new ServiceCollection();
        services.AddSampleModels();              // MappingManager registration
        services.AddSingleton<IMappingManager, MappingManager>();
        services.AddSingleton<SampleContext>();
        var provider = services.BuildServiceProvider();
        var ctx = provider.GetRequiredService<SampleContext>();
        var mapping = provider.GetRequiredService<IMappingManager>();

        // build schema from LINQ query
        var result = QueryAnalyzer.AnalyzeQuery<Order, Order>(src => src.Where(o => o.Quantity > 1));
        var schema = result.Schema!;

        var order = new Order { OrderId = 1, UserId = 10, ProductId = 5, Quantity = 2 };
        var (key, value) = mapping.ExtractKeyValue(order);
        await ctx.Set<Order>().AddAsync(order);
    }
}
