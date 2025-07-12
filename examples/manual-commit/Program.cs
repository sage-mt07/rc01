using Kafka.Ksql.Linq.Application;
using Kafka.Ksql.Linq.Core.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

[Topic("manual-commit-orders")]
public class ManualCommitOrder
{
    public int OrderId { get; set; }
    public decimal Amount { get; set; }
}

public class ManualCommitContext : KsqlContext
{
    protected override void OnModelCreating(IModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ManualCommitOrder>()
            .WithManualCommit();
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
            .BuildContext<ManualCommitContext>();

        var order = new ManualCommitOrder
        {
            OrderId = Random.Shared.Next(),
            Amount = 10m
        };

        await context.Set<ManualCommitOrder>().AddAsync(order);
        await Task.Delay(500);

        await context.Set<ManualCommitOrder>().ForEachAsync(async (IManualCommitMessage<ManualCommitOrder> msg) =>
        {
            try
            {
                Console.WriteLine($"Processing order {msg.Value.OrderId}: {msg.Value.Amount}");
                await msg.CommitAsync();
            }
            catch
            {
                await msg.NegativeAckAsync();
            }
        });
    }
}
