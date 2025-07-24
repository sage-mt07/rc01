using Kafka.Ksql.Linq.Core.Models;
using Kafka.Ksql.Linq.Core.Extensions;
using Kafka.Ksql.Linq.Messaging.Internal;
using Kafka.Ksql.Linq.SchemaRegistryTools;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;

namespace Kafka.Ksql.Linq.Application;

public static class MaterializationExtensions
{
    public static async Task EnsureMaterializedIfSchemaIsNewAsync<T>(this KsqlContext context) where T : class, new()
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        var client = context.GetSchemaRegistryClient();
        var model = context.GetEntityModels()[typeof(T)];
        var topicName = model.GetTopicName();
        var subject = $"{topicName}-value";
        var schema = DynamicSchemaGenerator.GetSchemaJson<T>();
        var result = await client.RegisterSchemaIfNewAsync(subject, schema);
        if (result.WasCreated)
        {
            var dummy = DummyObjectFactory.CreateDummy<T>();
            await context.Set<T>().AddAsync(dummy, new Dictionary<string, string> { ["is_dummy"] = "true" });
        }
    }
}
