using System;
using System.Collections.Generic;

namespace Kafka.Ksql.Linq.Messaging.Models;

public class DlqEnvelope
{
    /// <summary>
    /// Unique ID of the original message for idempotency and tracing.
    /// </summary>
    public Guid MessageId { get; set; }

    /// <summary>
    /// Original topic name where the message was published.
    /// </summary>
    public string Topic { get; set; } = string.Empty;

    /// <summary>
    /// Partition number of the original message.
    /// </summary>
    public int Partition { get; set; }

    /// <summary>
    /// Offset of the original message.
    /// </summary>
    public long Offset { get; set; }

    /// <summary>
    /// Timestamp of the original message in UTC.
    /// </summary>
    public DateTime TimestampUtc { get; set; }

    /// <summary>
    /// CLR type name of the key used when deserializing.
    /// </summary>
    public string KeyType { get; set; } = string.Empty;

    /// <summary>
    /// CLR type name of the value used when deserializing.
    /// </summary>
    public string ValueType { get; set; } = string.Empty;

    /// <summary>
    /// Raw bytes of the message that failed to process.
    /// </summary>
    public byte[] RawBytes { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Short form of the error message.
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// CLR type name of the thrown exception.
    /// </summary>
    public string ErrorType { get; set; } = string.Empty;

    /// <summary>
    /// Stack trace for debugging purposes. Optional.
    /// </summary>
    public string? StackTrace { get; set; }

    /// <summary>
    /// Restored Kafka headers for correlation or replay.
    /// Values are stored as strings for human readability.
    /// </summary>
    public Dictionary<string, string> Headers { get; set; } = new();
}

