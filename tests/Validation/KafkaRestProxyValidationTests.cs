using Kafka.Ksql.Linq;
using Kafka.Ksql.Linq.Configuration;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Core.Modeling;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Validation;

public class KafkaRestProxyValidationTests
{
    private class Order
    {
        public int Id { get; set; }
        public decimal Amount { get; set; }
    }

    private class TestContext : KsqlContext
    {
        public TestContext(KsqlDslOptions opt) : base(opt) { }
        protected override bool SkipSchemaRegistration => true;
        protected override void OnModelCreating(IModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Order>()
                .WithTopic("test-orders")
                .HasKey(o => o.Id);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Appsettings_ShouldMapCorrectly_AndSendKafkaMessage()
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("test_appsettings.json")
            .Build();

        var options = config.GetSection("KsqlDsl").Get<KsqlDslOptions>()!;
        using var ctx = new TestContext(options);

        var order = new Order { Id = 123, Amount = 456.78m };
        await ctx.Set<Order>().AddAsync(order);

        using var http = new HttpClient();
        var resp = await http.GetAsync("http://localhost:8082/topics/test-orders/partitions/0/messages?count=1");
        resp.EnsureSuccessStatusCode();

        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("123", body);
        Assert.Contains("456.78", body);
    }
}
