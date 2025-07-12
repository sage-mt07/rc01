using System;

namespace Kafka.Ksql.Linq.Serialization.Abstractions;
public static class AvroEntityConfigurationExtensions
{
    public static AvroEntityConfigurationBuilder<T> Configure<T>(this AvroEntityConfiguration configuration) where T : class
    {
        if (configuration.EntityType != typeof(T))
            throw new ArgumentException($"Configuration is for {configuration.EntityType.Name}, not {typeof(T).Name}");

        return new AvroEntityConfigurationBuilder<T>(configuration);
    }

    public static bool IsStreamType(this AvroEntityConfiguration configuration)
    {
        return configuration.GetStreamTableType() == "Stream";
    }

    public static bool IsTableType(this AvroEntityConfiguration configuration)
    {
        return configuration.GetStreamTableType() == "Table";
    }
}
