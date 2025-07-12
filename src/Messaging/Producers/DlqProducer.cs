using Confluent.Kafka;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Core.Attributes;
using Kafka.Ksql.Linq.Messaging.Contracts;
using Kafka.Ksql.Linq.Messaging.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kafka.Ksql.Linq.Messaging.Producers;

internal class DlqProducer : IErrorSink, IDisposable
{
    private readonly KafkaProducerManager _producerManager;
    private readonly string _dlqTopicName;
    private readonly DlqOptions _options;
    private bool _isInitialized = false;
    private bool _disposed = false;

    public bool IsAvailable => _isInitialized && !_disposed;

    public DlqProducer(KafkaProducerManager producerManager, DlqOptions? options = null)
    {
        _producerManager = producerManager ?? throw new ArgumentNullException(nameof(producerManager));
        _options = options ?? new DlqOptions();
        _dlqTopicName = _options.TopicName;
    }

    /// <summary>
    /// ✅ IErrorSink実装：エラーレコード処理（メインメソッド）
    /// </summary>
    public async Task HandleErrorAsync(ErrorContext errorContext, KafkaMessageContext messageContext)
    {
        if (!IsAvailable)
        {
            throw new InvalidOperationException("DlqProducer is not available. Call InitializeAsync() first.");
        }

        try
        {
            var dlqMessage = CreateDlqMessage(errorContext, messageContext);

            // DLQトピックに送信
            await SendToDlqTopicAsync(dlqMessage);

            // メトリクス更新
            _options.MetricsCallback?.Invoke(new DlqMetrics
            {
                TopicName = _dlqTopicName,
                OriginalTopic = messageContext.Tags.GetValueOrDefault("original_topic")?.ToString() ?? "unknown",
                ErrorType = errorContext.Exception.GetType().Name,
                ProcessedAt = DateTime.UtcNow
            });
        }
        catch (System.Exception ex)
        {
            throw new InvalidOperationException($"Failed to send error record to DLQ", ex);
        }
    }

    /// <summary>
    /// ✅ IErrorSink実装：エラーレコード処理（オーバーロード）
    /// </summary>
    public async Task HandleErrorAsync(ErrorContext errorContext)
    {
        var defaultMessageContext = new KafkaMessageContext
        {
            MessageId = Guid.NewGuid().ToString(),
            Tags = new Dictionary<string, object>
            {
                ["original_topic"] = "unknown",
                ["error_phase"] = errorContext.ErrorPhase,
                ["processed_at"] = DateTime.UtcNow
            }
        };

        await HandleErrorAsync(errorContext, defaultMessageContext);
    }

    /// <summary>
    /// ✅ IErrorSink実装：初期化
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_isInitialized)
            return;

        try
        {
            // Producer準備等の初期化処理
            await Task.CompletedTask; // 現在は特別な初期化不要
            _isInitialized = true;
        }
        catch (System.Exception ex)
        {
            throw new InvalidOperationException("Failed to initialize DlqProducer", ex);
        }
    }

    /// <summary>
    /// ✅ IErrorSink実装：クリーンアップ
    /// </summary>
    public async Task CleanupAsync()
    {
        if (_disposed)
            return;

        try
        {
            // リソースのクリーンアップ
            await Task.CompletedTask;
        }
        catch (System.Exception ex)
        {
            Console.WriteLine($"Error during DlqProducer cleanup: {ex.Message}");
        }
    }

    /// <summary>
    /// DLQメッセージの作成
    /// </summary>
    private DlqEnvelope CreateDlqMessage(ErrorContext errorContext, KafkaMessageContext messageContext)
    {
        var messageId = Guid.TryParse(messageContext.MessageId, out var id)
            ? id
            : Guid.NewGuid();

        var topic = messageContext.Tags.GetValueOrDefault("original_topic")?.ToString()
            ?? messageContext.Tags.GetValueOrDefault("topic")?.ToString()
            ?? string.Empty;

        var partition = messageContext.Tags.GetValueOrDefault("original_partition") as int?
            ?? messageContext.Tags.GetValueOrDefault("partition") as int?
            ?? -1;

        var offset = messageContext.Tags.GetValueOrDefault("original_offset") as long?
            ?? messageContext.Tags.GetValueOrDefault("offset") as long?
            ?? -1L;

        var timestamp = messageContext.Tags.TryGetValue("timestamp_utc", out var tsObj) && tsObj is DateTime dt
            ? dt
            : DateTime.UtcNow;

        var keyType = messageContext.Tags.GetValueOrDefault("key_type")?.ToString() ?? string.Empty;
        var valueType = messageContext.Tags.GetValueOrDefault("value_type")?.ToString() ?? string.Empty;

        var headers = new Dictionary<string, string>();
        foreach (var kvp in messageContext.Headers)
        {
            headers[kvp.Key] = kvp.Value?.ToString() ?? string.Empty;
        }

        return new DlqEnvelope
        {
            MessageId = messageId,
            Topic = topic,
            Partition = partition,
            Offset = offset,
            TimestampUtc = timestamp,
            KeyType = keyType,
            ValueType = valueType,
            RawBytes = errorContext.OriginalMessage as byte[] ?? Array.Empty<byte>(),
            ErrorMessage = errorContext.Exception.Message,
            ErrorType = errorContext.Exception.GetType().Name,
            StackTrace = errorContext.Exception.StackTrace,
            Headers = headers
        };
    }

    /// <summary>
    /// DLQトピックへの送信
    /// </summary>
    private async Task SendToDlqTopicAsync(DlqEnvelope dlqMessage)
    {
        await _producerManager.SendAsync(_dlqTopicName, dlqMessage);
    }

    private static Dictionary<string, object> ConvertHeadersToDictionary(Headers? headers)
    {
        var dict = new Dictionary<string, object>();
        if (headers == null) return dict;

        foreach (var header in headers)
        {
            if (header.GetValueBytes() != null)
            {
                var value = System.Text.Encoding.UTF8.GetString(header.GetValueBytes());
                dict[header.Key] = value;
            }
        }

        return dict;
    }

    /// <summary>
    /// デシリアライズ失敗データをDLQへ送信
    /// </summary>
    public async Task SendAsync(
        byte[]? data,
        System.Exception exception,
        string originalTopic,
        int partition,
        long offset,
        DateTime timestampUtc,
        Headers? headers,
        string keyType,
        string valueType)
    {
        var context = new KafkaMessageContext
        {
            MessageId = Guid.NewGuid().ToString(),
            Headers = ConvertHeadersToDictionary(headers),
            Tags = new Dictionary<string, object>
            {
                ["original_topic"] = originalTopic,
                ["original_partition"] = partition,
                ["original_offset"] = offset,
                ["timestamp_utc"] = timestampUtc,
                ["key_type"] = keyType,
                ["value_type"] = valueType,
                ["error_phase"] = "Deserialization"
            }
        };

        var errorContext = new ErrorContext
        {
            Exception = exception,
            OriginalMessage = data,
            AttemptCount = 1,
            FirstAttemptTime = DateTime.UtcNow,
            LastAttemptTime = DateTime.UtcNow,
            ErrorPhase = "Deserialization"
        };

        await HandleErrorAsync(errorContext, context);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            CleanupAsync().GetAwaiter().GetResult();
            _disposed = true;
        }
    }
}

/// <summary>
/// DLQ設定オプション
/// </summary>
public class DlqOptions
{
    /// <summary>
    /// 共通DLQトピック名
    /// </summary>
    [DefaultValue("dead.letter.queue")]
    public string TopicName { get; set; } = "dead.letter.queue";

    public bool EnableCompression { get; set; } = true;
    [DefaultValue(3)]
    public int MaxRetryAttempts { get; set; } = 3;
    [DefaultValue("00:00:01")]
    public TimeSpan RetryInterval { get; set; } = TimeSpan.FromSeconds(1);
    public Action<DlqMetrics>? MetricsCallback { get; set; }
}

/// <summary>
/// DLQメトリクス情報
/// </summary>
public class DlqMetrics
{
    public string TopicName { get; set; } = string.Empty;
    public string OriginalTopic { get; set; } = string.Empty;
    public string ErrorType { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; }
}

/// <summary>
/// DLQ例外情報
/// </summary>
public class DlqExceptionInfo
{
    public string Type { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? StackTrace { get; set; }
    public string? InnerException { get; set; }
}
