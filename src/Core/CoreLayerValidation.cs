using Kafka.Ksql.Linq.Core.Abstractions;
// ✅ 修正: 重複削除により ValidationResult を Core.Abstractions から参照
// using Kafka.Ksql.Linq.Core.Validation; // ❌ 削除: 重複定義を参照していた
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Kafka.Ksql.Linq.Core;

/// <summary>
/// Core層設計制約の検証 - Phase2修正版
/// 設計理由：依存関係の一方向性確保
/// 修正理由：重複削除による参照更新
/// </summary>
internal static class CoreLayerValidation
{
    private static readonly string[] ForbiddenNamespaces = new[]
    {
        "Kafka.Ksql.Linq.Communication",
        "Kafka.Ksql.Linq.Messaging",
        "Kafka.Ksql.Linq.Monitoring",
        "Kafka.Ksql.Linq.Avro",
        "Kafka.Ksql.Linq.Services"
    };

    public static ValidationResult ValidateCoreDependencies()  // ✅ 修正: Core.Abstractions.ValidationResult 使用
    {
        var result = new ValidationResult { IsValid = true };  // ✅ 修正: Core.Abstractions.ValidationResult 使用
        var coreAssembly = typeof(IKsqlContext).Assembly;
        var coreTypes = coreAssembly.GetTypes()
            .Where(t => t.Namespace?.StartsWith("Kafka.Ksql.Linq.Core") == true);

        foreach (var type in coreTypes)
        {
            ValidateTypedependencies(type, result);
        }

        return result;
    }

    private static void ValidateTypedependencies(Type type, ValidationResult result)  // ✅ 修正: Core.Abstractions.ValidationResult 使用
    {
        var dependencies = type.GetReferencedTypes();

        foreach (var dependency in dependencies)
        {
            if (dependency.Namespace != null &&
                ForbiddenNamespaces.Any(ns => dependency.Namespace.StartsWith(ns)))
            {
                result.IsValid = false;
                result.Errors.Add(
                    $"Core type {type.Name} has forbidden dependency on {dependency.Namespace}.{dependency.Name}");
            }
        }
    }

    private static IEnumerable<Type> GetReferencedTypes(this Type type)
    {
        var referencedTypes = new HashSet<Type>();

        // Field types
        foreach (var field in type.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance))
        {
            referencedTypes.Add(field.FieldType);
        }

        // Property types
        foreach (var property in type.GetProperties())
        {
            referencedTypes.Add(property.PropertyType);
        }

        // Method parameter and return types
        foreach (var method in type.GetMethods())
        {
            referencedTypes.Add(method.ReturnType);
            foreach (var param in method.GetParameters())
            {
                referencedTypes.Add(param.ParameterType);
            }
        }

        return referencedTypes.Where(t => t.Assembly != type.Assembly);
    }
}