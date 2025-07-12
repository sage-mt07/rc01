using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Core.Attributes;
using Kafka.Ksql.Linq.Messaging.Contracts;
using System;
using System.Threading.Tasks;

namespace Kafka.Ksql.Linq.Messaging.Internal;
public class ErrorHandlingContext
{
    /// <summary>
    /// エラー発生時のアクション
    /// </summary>
    [DefaultValue(ErrorAction.Skip)]
    public ErrorAction ErrorAction { get; set; } = ErrorAction.Skip;

    /// <summary>
    /// リトライ回数
    /// </summary>
    [DefaultValue(3)]
    public int RetryCount { get; set; } = 3;

    /// <summary>
    /// リトライ間隔
    /// </summary>
    [DefaultValue("00:00:01")]
    public TimeSpan RetryInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// 現在の試行回数（内部管理用）
    /// </summary>
    public int CurrentAttempt { get; set; } = 0;

    /// <summary>
    /// エラーシンク（DLQ送信等）
    /// </summary>
    public IErrorSink? ErrorSink { get; set; }

    /// <summary>
    /// カスタムエラーハンドラー（型安全版）
    /// </summary>
    public Func<ErrorContext, object, bool>? CustomHandler { get; set; }

    /// <summary>
    /// エラーハンドリング実行
    /// </summary>
    /// <param name="originalItem">元のアイテム</param>
    /// <param name="exception">発生した例外</param>
    /// <param name="messageContext">メッセージコンテキスト</param>
    /// <returns>処理を継続するかどうか（false=スキップ、true=継続/リスロー）</returns>
    public async Task<bool> HandleErrorAsync<T>(T originalItem, Exception exception, KafkaMessageContext messageContext)
    {
        // カスタムハンドラーが設定されている場合は優先実行
        if (ErrorAction == ErrorAction.Skip && CustomHandler != null)
        {
            var errorContext = new ErrorContext
            {
                Exception = exception,
                OriginalMessage = originalItem,
                AttemptCount = CurrentAttempt,
                FirstAttemptTime = DateTime.UtcNow.AddSeconds(-CurrentAttempt * RetryInterval.TotalSeconds),
                LastAttemptTime = DateTime.UtcNow,
                ErrorPhase = "Processing"
            };

            try
            {
                return CustomHandler(errorContext, originalItem!);
            }
            catch (Exception handlerEx)
            {
                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] CUSTOM_HANDLER_ERROR: {handlerEx.Message}");
                return false; // カスタムハンドラーでエラーが発生した場合はスキップ
            }
        }

        switch (ErrorAction)
        {
            case ErrorAction.Skip:
                // エラーログ出力してスキップ
                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] SKIP: {exception.Message}");
                return false; // スキップ

            case ErrorAction.Retry:
                // リトライはProcessItemWithErrorHandling側で制御
                // ここに到達するのは最終試行後なので、スキップまたはDLQ送信
                if (ErrorSink != null)
                {
                    await SendToDlqAsync(originalItem, exception, messageContext);
                }
                return false; // スキップ

            case ErrorAction.DLQ:
                // DLQ送信
                if (ErrorSink != null)
                {
                    await SendToDlqAsync(originalItem, exception, messageContext);
                }
                else
                {
                    Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] DLQ: No ErrorSink configured, skipping item");
                }
                return false; // スキップ

            default:
                // 未知のアクションの場合はスキップ
                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] UNKNOWN ERROR ACTION: {ErrorAction}, skipping item");
                return false;
        }
    }

    /// <summary>
    /// DLQ送信処理
    /// </summary>
    private async Task SendToDlqAsync<T>(T originalItem, Exception exception, KafkaMessageContext messageContext)
    {
        try
        {
            if (ErrorSink != null)
            {
                var errorContext = new ErrorContext
                {
                    Exception = exception,
                    OriginalMessage = originalItem,
                    AttemptCount = CurrentAttempt,
                    FirstAttemptTime = DateTime.UtcNow.AddSeconds(-CurrentAttempt * RetryInterval.TotalSeconds),
                    LastAttemptTime = DateTime.UtcNow,
                    ErrorPhase = "Processing"
                };

                await ErrorSink.HandleErrorAsync(errorContext, messageContext);
                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] DLQ: Sent error record to DLQ");
            }
        }
        catch (Exception dlqEx)
        {
            Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] DLQ ERROR: Failed to send to DLQ - {dlqEx.Message}");
        }
    }
}
