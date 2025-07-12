using Kafka.Ksql.Linq.Core.Modeling;
using Kafka.Ksql.Linq.Entities.Samples.Models;
using Kafka.Ksql.Linq.Mapping;
using Microsoft.Extensions.DependencyInjection;

namespace Kafka.Ksql.Linq.Entities.Samples;

public static class SampleModelRegistration
{
    /// <summary>
    /// Registers sample models and MappingManager into the DI container.
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

        // create the mapping manager and register the models immediately so
        // consumers can use it without also resolving ModelBuilder
        var manager = new MappingManager();
        manager.Register<User>(builder.GetEntityModel<User>()!);
        manager.Register<Product>(builder.GetEntityModel<Product>()!);
        manager.Register<Order>(builder.GetEntityModel<Order>()!);

        services.AddSingleton<IMappingManager>(manager);
        services.AddSingleton(builder);

        return services;
    }
}
