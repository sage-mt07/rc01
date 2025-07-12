using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Core.Modeling;

namespace Samples.TopicFluentApiExtension;

// DecimalPrecision 属性を Fluent API に置き換える最小例
public static class Example4_DecimalPrecision
{
    private class Payment
    {
        public int Id { get; set; }
        public decimal Amount { get; set; }
    }

    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<Payment>()
            .HasKey(p => p.Id)
            .AsTable("payments")
            .WithDecimalPrecision(p => p.Amount, precision: 18, scale: 2);
    }
}
