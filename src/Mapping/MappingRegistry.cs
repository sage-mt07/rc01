using Kafka.Ksql.Linq.Core.Models;
using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Linq;
using System.Reflection.Emit;

namespace Kafka.Ksql.Linq.Mapping;

/// <summary>
/// Provides registration and lookup of dynamically generated key/value types
/// based on PropertyMeta information.
/// </summary>
public class MappingRegistry
{
    private readonly ConcurrentDictionary<Type, KeyValueTypeMapping> _mappings = new();
    private readonly ModuleBuilder _moduleBuilder;

    public MappingRegistry()
    {
        var asmName = new AssemblyName("KafkaKsqlDynamicMappings");
        var asmBuilder = AssemblyBuilder.DefineDynamicAssembly(asmName, AssemblyBuilderAccess.Run);
        _moduleBuilder = asmBuilder.DefineDynamicModule("Main");
    }

    public KeyValueTypeMapping Register(
        Type pocoType,
        PropertyMeta[] keyProperties,
        PropertyMeta[] valueProperties)
    {
        var ns = pocoType.Namespace?.ToLower() ?? string.Empty;
        var baseName = pocoType.Name.ToLower();

        var keyType = CreateType(ns, $"{baseName}-key", keyProperties);
        var valueType = CreateType(ns, $"{baseName}-value", valueProperties);

        var keyTypeProps = keyProperties
            .Select(p => keyType.GetProperty(p.Name)!)
            .ToArray();
        var valueTypeProps = valueProperties
            .Select(p => valueType.GetProperty(p.Name)!)
            .ToArray();

        var mapping = new KeyValueTypeMapping
        {
            KeyType = keyType,
            KeyProperties = keyProperties,
            KeyTypeProperties = keyTypeProps,
            ValueType = valueType,
            ValueProperties = valueProperties,
            ValueTypeProperties = valueTypeProps
        };
        _mappings[pocoType] = mapping;
        return mapping;
    }

    public KeyValueTypeMapping GetMapping(Type pocoType)
    {
        if (_mappings.TryGetValue(pocoType, out var mapping))
        {
            return mapping;
        }
        throw new InvalidOperationException($"Mapping for {pocoType.FullName} is not registered.");
    }

    private Type CreateType(string ns, string name, PropertyMeta[] properties)
    {
        var typeBuilder = _moduleBuilder.DefineType($"{ns}.{name}", TypeAttributes.Public | TypeAttributes.Class);
        foreach (var meta in properties)
        {
            var field = typeBuilder.DefineField($"_{meta.Name}", meta.PropertyType, FieldAttributes.Private);
            var property = typeBuilder.DefineProperty(meta.Name, PropertyAttributes.None, meta.PropertyType, null);
            var getMethod = typeBuilder.DefineMethod(
                $"get_{meta.Name}",
                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName,
                meta.PropertyType,
                Type.EmptyTypes);
            var ilGet = getMethod.GetILGenerator();
            ilGet.Emit(OpCodes.Ldarg_0);
            ilGet.Emit(OpCodes.Ldfld, field);
            ilGet.Emit(OpCodes.Ret);
            var setMethod = typeBuilder.DefineMethod(
                $"set_{meta.Name}",
                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName,
                null,
                new[] { meta.PropertyType });
            var ilSet = setMethod.GetILGenerator();
            ilSet.Emit(OpCodes.Ldarg_0);
            ilSet.Emit(OpCodes.Ldarg_1);
            ilSet.Emit(OpCodes.Stfld, field);
            ilSet.Emit(OpCodes.Ret);
            property.SetGetMethod(getMethod);
            property.SetSetMethod(setMethod);
        }
        return typeBuilder.CreateType()!;
    }
}
