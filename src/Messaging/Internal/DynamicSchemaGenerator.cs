using Avro;
using Avro.Reflect;
using System;

namespace Kafka.Ksql.Linq.Messaging.Internal;

/// <summary>
/// Utility to dynamically generate Avro schemas from C# types at runtime.
/// </summary>
internal static class DynamicSchemaGenerator
{
    /// <summary>
    /// Generate Avro <see cref="Schema"/> for the specified type using
    /// <see cref="SchemaBuilder"/> with <see cref="ReflectionSchemaBuilder"/>.
    /// </summary>
    public static Schema GetSchema(Type type)
        => SchemaBuilder.GetSchema(type, new ReflectionSchemaBuilder());

    /// <summary>
    /// Generate Avro <see cref="Schema"/> for the generic type parameter.
    /// </summary>
    public static Schema GetSchema<T>() where T : class => GetSchema(typeof(T));

    /// <summary>
    /// Generate the schema JSON string for the specified type.
    /// </summary>
    public static string GetSchemaJson(Type type) => GetSchema(type).ToString();

    /// <summary>
    /// Generate the schema JSON string for the generic type parameter.
    /// </summary>
    public static string GetSchemaJson<T>() where T : class => GetSchemaJson(typeof(T));
}
