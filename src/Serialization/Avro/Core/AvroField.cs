namespace Kafka.Ksql.Linq.Serialization.Avro.Core;


internal class AvroField
{
    public string Name { get; set; } = string.Empty;
    public object Type { get; set; } = string.Empty;
    public string? Doc { get; set; }
    public object? Default { get; set; }
}
