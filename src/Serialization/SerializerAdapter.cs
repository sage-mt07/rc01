using Confluent.Kafka;
using System;

namespace Kafka.Ksql.Linq.Serialization;

internal static class SerializerAdapter
{
    public static ISerializer<object> Create<T>(ISerializer<T> inner)
    {
        if (inner == null) throw new ArgumentNullException(nameof(inner));
        return new Adapter<T>(inner);
    }

    private class Adapter<T> : ISerializer<object>
    {
        private readonly ISerializer<T> _inner;
        public Adapter(ISerializer<T> inner) => _inner = inner;
        public byte[] Serialize(object data, SerializationContext context)
        {
            if (data is not T t)
                throw new ArgumentException($"Expected data of type {typeof(T).Name}, got {data?.GetType().Name}");
            return _inner.Serialize(t, context);
        }
    }
}

internal static class DeserializerAdapter
{
    public static IDeserializer<object> Create<T>(IDeserializer<T> inner)
    {
        if (inner == null) throw new ArgumentNullException(nameof(inner));
        return new Adapter<T>(inner);
    }

    private class Adapter<T> : IDeserializer<object>
    {
        private readonly IDeserializer<T> _inner;
        public Adapter(IDeserializer<T> inner) => _inner = inner;
        public object Deserialize(ReadOnlySpan<byte> data, bool isNull, SerializationContext context)
        {
            return _inner.Deserialize(data, isNull, context)!;
        }
    }
}
