using System.Collections.Generic;
using System.Text.Json;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Serialization.Avro.Core;
using Kafka.Ksql.Linq.Tests;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Unit;

public class AvroSchemaTests
{
    private class OrderValue
    {
        public int CustomerId { get; set; }
        public int Id { get; set; }
        public string Region { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public bool IsHighPriority { get; set; }
        public int Count { get; set; }
    }

    [Fact]
    public void OrderValue_AvroSchema_ShouldMatch_RegisteredSchema()
    {
        var generated = UnifiedSchemaGenerator.GenerateSchema(typeof(OrderValue));
        var registered = CreateExpectedSchema();
        JsonAssert.Equal(registered, generated);
    }

    private static string CreateExpectedSchema()
    {
        var schema = new AvroSchema
        {
            Type = "record",
            Name = nameof(OrderValue),
            Namespace = typeof(OrderValue).Namespace,
            Fields = new List<AvroField>
            {
                new() { Name = nameof(OrderValue.CustomerId), Type = "int" },
                new() { Name = nameof(OrderValue.Id), Type = "int" },
                new() { Name = nameof(OrderValue.Region), Type = "string" },
                new() { Name = nameof(OrderValue.Amount), Type = new { type = "bytes", logicalType = "decimal", precision = 18, scale = 4 } },
                new() { Name = nameof(OrderValue.IsHighPriority), Type = "boolean" },
                new() { Name = nameof(OrderValue.Count), Type = "int" }
            }
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        return JsonSerializer.Serialize(schema, options);
    }
}
