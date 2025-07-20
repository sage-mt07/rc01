using Avro;
using Chr.Avro.Abstract;
using Chr.Avro.Representation;
using AvroSchema = Avro.Schema;
using AbstractSchema = Chr.Avro.Abstract.Schema;
using System;
using System.IO;
using System.Text;

namespace Kafka.Ksql.Linq.Messaging.Internal;

/// <summary>
/// Utility to dynamically generate Avro schemas from C# types at runtime.
/// </summary>
internal static class DynamicSchemaGenerator
{
    private static readonly SchemaBuilder _schemaBuilder = new();
    private static readonly JsonSchemaWriter _schemaWriter = new();

    /// <summary>
    /// Generate Apache.Avro <see cref="Schema"/> for the specified type.
    /// </summary>
    public static AvroSchema GetSchema(Type type)
    {
        var abstractSchema = _schemaBuilder.BuildSchema(type);
        using var ms = new MemoryStream();
        _schemaWriter.Write(abstractSchema, ms);
        var json = Encoding.UTF8.GetString(ms.ToArray());
        return AvroSchema.Parse(json);
    }

    /// <summary>
    /// Generate Apache.Avro <see cref="Schema"/> for the generic type parameter.
    /// </summary>
    public static AvroSchema GetSchema<T>() => GetSchema(typeof(T));

    /// <summary>
    /// Generate the schema JSON string for the specified type.
    /// </summary>
    public static string GetSchemaJson(Type type)
    {
        var abstractSchema = _schemaBuilder.BuildSchema(type);
        using var ms = new MemoryStream();
        _schemaWriter.Write(abstractSchema, ms);
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    /// <summary>
    /// Generate the schema JSON string for the generic type parameter.
    /// </summary>
    public static string GetSchemaJson<T>() => GetSchemaJson(typeof(T));
}
