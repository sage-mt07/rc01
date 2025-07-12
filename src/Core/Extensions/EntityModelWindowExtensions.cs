using Kafka.Ksql.Linq.Core.Abstractions;
using System;
using System.Linq;
using System.Reflection;

namespace Kafka.Ksql.Linq.Core.Extensions;

internal static class EntityModelWindowExtensions
{
    internal static PropertyInfo? GetTimestampProperty(this EntityModel entityModel)
    {
        return entityModel.AllProperties
            .FirstOrDefault(p => p.PropertyType == typeof(DateTime) || p.PropertyType == typeof(DateTimeOffset));
    }

    internal static bool IsWindowCapable(this EntityModel entityModel)
    {
        return entityModel.GetTimestampProperty() != null;
    }

    internal static bool HasWindowConfiguration(this EntityModel entityModel)
    {
        return entityModel.ValidationResult?.Warnings
            .Any(w => w.Contains("Window configuration")) == true;
    }

    internal static ValidationResult ValidateWindowSupport(this EntityModel entityModel)
    {
        var result = new ValidationResult { IsValid = true };

        var timestampProperty = entityModel.GetTimestampProperty();
        if (timestampProperty == null)
        {
            result.IsValid = false;
            result.Errors.Add($"Entity {entityModel.EntityType.Name} requires exactly one DateTime or DateTimeOffset property for window operations");
            return result;
        }

        var propType = timestampProperty.PropertyType;
        var validTypes = new[] { typeof(DateTime), typeof(DateTime?), typeof(DateTimeOffset), typeof(DateTimeOffset?) };

        if (!validTypes.Contains(propType))
        {
            result.IsValid = false;
            result.Errors.Add($"Property {timestampProperty.Name} used as timestamp must be DateTime or DateTimeOffset type, but was {propType.Name}");
        }

        return result;
    }
}
