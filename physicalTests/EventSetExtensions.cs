using Kafka.Ksql.Linq;
using Kafka.Ksql.Linq.Application;
using Kafka.Ksql.Linq.Messaging.Producers;
using Kafka.Ksql.Linq.Core.Abstractions;
using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Kafka.Ksql.Linq.Tests.Integration;

public static class EventSetExtensions
{
    public static async Task AddAsync<T>(this IEntitySet<T> set, T entity, KafkaMessageContext context, CancellationToken cancellationToken = default) where T : class
    {
        if (set == null) throw new ArgumentNullException(nameof(set));
        if (entity == null) throw new ArgumentNullException(nameof(entity));
        var field = set.GetType().GetField("_context", BindingFlags.NonPublic | BindingFlags.Instance);
        var ksqlContext = (KsqlContext?)field?.GetValue(set);
        if (ksqlContext == null)
            throw new InvalidOperationException("Invalid entity set instance");
        var manager = Kafka.Ksql.Linq.Tests.PrivateAccessor.InvokePrivate<KafkaProducerManager>(ksqlContext!, "GetProducerManager", Type.EmptyTypes);
        await (await manager.GetProducerAsync<T>()).SendAsync(entity, context, cancellationToken);
    }
}
