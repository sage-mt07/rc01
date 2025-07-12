using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Confluent.SchemaRegistry;

namespace Kafka.Ksql.Linq.Tests.Serialization;

#nullable enable

internal class FakeSchemaRegistryClient : DispatchProxy
{
    public bool Disposed { get; private set; }
    public List<string> RegisterSubjects { get; } = new();
    public int RegisterReturn { get; set; } = 1;
    public bool CompatibilityResult { get; set; } = true;
    public List<int> VersionsResult { get; set; } = new();
    public int LatestVersion { get; set; } = 1;
    public string SchemaString { get; set; } = "schema";
    public Dictionary<string, string> RegisteredSchemaStrings { get; } = new();
    public int MaxCachedSchemas { get; set; } = 1000;

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        switch (targetMethod?.Name)
        {
            case nameof(ISchemaRegistryClient.RegisterSchemaAsync):
            {
                var regSubject = (string)args![0]!;
                var schemaArg = args[1]!;
                string schemaString = schemaArg switch
                {
                    Schema s => s.SchemaString,
                    string s => s,
                    _ => throw new InvalidCastException()
                };
                RegisterSubjects.Add(regSubject);
                SchemaString = schemaString;
                RegisteredSchemaStrings[regSubject] = schemaString;
                return Task.FromResult(RegisterReturn);
            }
            case nameof(ISchemaRegistryClient.IsCompatibleAsync):
                return Task.FromResult(CompatibilityResult);
            case nameof(ISchemaRegistryClient.GetSubjectVersionsAsync):
                return Task.FromResult(VersionsResult);
            case nameof(ISchemaRegistryClient.GetRegisteredSchemaAsync):
            {
                var regSubject = (string)args![0]!;
                var version = (int)args[1]!;
                var regType = typeof(Schema).Assembly.GetType("Confluent.SchemaRegistry.RegisteredSchema")!;
                var schemaVersion = version == -1 ? LatestVersion : version;
                var schemaText = RegisteredSchemaStrings.TryGetValue(regSubject, out var s) ? s : SchemaString;
                var obj = Activator.CreateInstance(regType, regSubject, schemaVersion, RegisterReturn, schemaText, SchemaType.Avro, null)!;
                var fromResult = typeof(Task)
                    .GetMethod("FromResult")!
                    .MakeGenericMethod(regType)
                    .Invoke(null, new[] { obj });
                return fromResult!;
            }
            case nameof(ISchemaRegistryClient.GetSchemaAsync):
            {
                // Return Schema object for provided id using stored SchemaString
                var schemaType = typeof(Schema);
                var ctor = schemaType.GetConstructor(new[] { typeof(string), typeof(SchemaType) });
                var schemaObj = ctor != null
                    ? (Schema)ctor.Invoke(new object[] { SchemaString, SchemaType.Avro })
                    : new Schema(SchemaString, SchemaType.Avro);
                return Task.FromResult(schemaObj);
            }
            case nameof(ISchemaRegistryClient.ConstructKeySubjectName):
            {
                var topic = (string)args![0]!;
                return $"{topic}-key";
            }
            case nameof(ISchemaRegistryClient.ConstructValueSubjectName):
            {
                var topic = (string)args![0]!;
                return $"{topic}-value";
            }
            case "get_" + nameof(ISchemaRegistryClient.MaxCachedSchemas):
                return MaxCachedSchemas;
            case nameof(IDisposable.Dispose):
                Disposed = true;
                return null;
        }
        throw new NotImplementedException(targetMethod?.Name);
    }
}
