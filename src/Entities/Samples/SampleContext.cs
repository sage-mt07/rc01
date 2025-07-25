using System;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Configuration;
using Kafka.Ksql.Linq.Core.Modeling;
using Kafka.Ksql.Linq.Entities.Samples.Models;

namespace Kafka.Ksql.Linq.Entities.Samples;

/// <summary>
/// Minimal context demonstrating OnModelCreating based registration.
/// EntitySet creation is not implemented in this sample.
/// </summary>
public class SampleContext : KsqlContext
{
    public SampleContext() : base(new KsqlDslOptions()) { }
    protected override IEntitySet<T> CreateEntitySet<T>(EntityModel entityModel)
    {
        throw new NotImplementedException();
    }

    protected override void OnModelCreating(IModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(readOnly: true)
            .WithTopic("users")
            .HasKey(u => u.Id);

        modelBuilder.Entity<Product>()
            .WithTopic("products")
            .HasKey(p => p.ProductId);

        modelBuilder.Entity<Order>(writeOnly: true)
            .WithTopic("orders")
            .HasKey(o => new { o.OrderId, o.UserId });
    }
}
