using Confluent.Kafka;
using System;

namespace Kafka.Ksql.Linq.Messaging.Internal;

internal static class SerializerAdapters
{
    public static ISerializer<object> ToObjectSerializer<T>(ISerializer<T> inner)
    {
        if (inner == null) throw new ArgumentNullException(nameof(inner));
        return new ObjectSerializer<T>(inner);
    }

    private class ObjectSerializer<T> : ISerializer<object>
    {
        private readonly ISerializer<T> _inner;
        public ObjectSerializer(ISerializer<T> inner) => _inner = inner;
        public byte[] Serialize(object data, SerializationContext context)
        {
            if (data is not T t)
                throw new ArgumentException($"Expected data of type {typeof(T).Name}, got {data?.GetType().Name}");
            return _inner.Serialize(t, context);
        }
    }

    public static IDeserializer<object> ToObjectDeserializer<T>(IDeserializer<T> inner)
    {
        if (inner == null) throw new ArgumentNullException(nameof(inner));
        return new ObjectDeserializer<T>(inner);
    }

    private class ObjectDeserializer<T> : IDeserializer<object>
    {
        private readonly IDeserializer<T> _inner;
        public ObjectDeserializer(IDeserializer<T> inner) => _inner = inner;
        public object Deserialize(ReadOnlySpan<byte> data, bool isNull, SerializationContext context)
            => _inner.Deserialize(data, isNull, context)!;
    }
}
