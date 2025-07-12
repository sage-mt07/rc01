using Kafka.Ksql.Linq.Serialization.Avro.Core;

namespace Kafka.Ksql.Linq.Application;

internal static class AvroSchemaInfoExtensions
{
    public static string GetKeySubject(this AvroSchemaInfo schemaInfo)
    {
        return $"{schemaInfo.TopicName}-key";
    }

    public static string GetValueSubject(this AvroSchemaInfo schemaInfo)
    {
        return $"{schemaInfo.TopicName}-value";
    }

    public static string GetStreamTableType(this AvroSchemaInfo schemaInfo)
    {
        return schemaInfo.HasCustomKey ? "Table" : "Stream";
    }

    public static string GetKeyTypeName(this AvroSchemaInfo schemaInfo)
    {
        if (!schemaInfo.HasCustomKey)
            return "string";

        if (schemaInfo.KeyProperties?.Length == 1)
            return schemaInfo.KeyProperties[0].PropertyType.Name;

        return "CompositeKey";
    }

}
