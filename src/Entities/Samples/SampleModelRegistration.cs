using Kafka.Ksql.Linq.Core.Modeling;
using Kafka.Ksql.Linq.Entities.Samples.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Kafka.Ksql.Linq.Entities.Samples;

public static class SampleModelRegistration
{
    /// <summary>
    /// Registers sample models into the DI container.
    /// </summary>
    public static IServiceCollection AddSampleModels(this IServiceCollection services)
    {
        // create a model builder and register sample entities
        var builder = new ModelBuilder();

        builder.Entity<User>()
            .WithTopic("users")
            .HasKey(u => u.Id);

        builder.Entity<Product>()
            .WithTopic("products")
            .HasKey(p => p.ProductId);

        builder.Entity<Order>()
            .WithTopic("orders")
            .HasKey(o => new { o.OrderId, o.UserId });

        services.AddSingleton(builder);

        return services;
    }
}
