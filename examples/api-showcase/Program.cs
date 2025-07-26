using Kafka.Ksql.Linq;
using Kafka.Ksql.Linq.Core.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

using System.Linq;
[Topic("api-showcase")]
public class ApiMessage
{
    public int Id { get; set; }

    [AvroTimestamp]
    public DateTime CreatedAt { get; set; }

    public string Category { get; set; } = string.Empty;
}

public class CategoryCount
{
    public string Key { get; set; } = string.Empty;
    public long Count { get; set; }
}

public class ApiContext : KsqlContext
{
    protected override void OnModelCreating(IModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApiMessage>();
        modelBuilder.Entity<CategoryCount>()
            .HasQueryFrom<ApiMessage>(q => q.Where(m => m.Category == "A")
                             .GroupBy(m => m.Category)
                             .Select(g => new CategoryCount { Key = g.Key, Count = g.Count() }));
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
            .BuildContext<ApiContext>();

        var message = new ApiMessage
        {
            Id = Random.Shared.Next(),
            CreatedAt = DateTime.UtcNow,
            Category = "A"
        };

        await context.Set<ApiMessage>().AddAsync(message);
        // wait briefly for message to be published
        await Task.Delay(500);

        await context.Set<CategoryCount>()
            .OnError(ErrorAction.Skip)
            .WithRetry(2)
            .ForEachAsync(r =>
            {
                Console.WriteLine($"Category {r.Key}: {r.Count}");
                return Task.CompletedTask;
            });
    }
}
