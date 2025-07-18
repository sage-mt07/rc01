using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Core.Attributes;
using System;
using System.Threading.Tasks;

namespace Kafka.Ksql.Linq.Messaging.Internal;
public class ErrorHandlingContext
{
    /// <summary>
    /// Action to take when an error occurs
    /// </summary>
    [DefaultValue(ErrorAction.Skip)]
    public ErrorAction ErrorAction { get; set; } = ErrorAction.Skip;

    /// <summary>
    /// Number of retry attempts
    /// </summary>
    [DefaultValue(3)]
    public int RetryCount { get; set; } = 3;

    /// <summary>
    /// Interval between retries
    /// </summary>
    [DefaultValue("00:00:01")]
    public TimeSpan RetryInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Current attempt count (internal use)
    /// </summary>
    public int CurrentAttempt { get; set; } = 0;

    /// <summary>
    /// Event triggered when an error occurs
    /// </summary>
    public event Func<ErrorContext, KafkaMessageContext, Task>? ErrorOccurred;

    /// <summary>
    /// Custom error handler (type-safe)
    /// </summary>
    public Func<ErrorContext, object, bool>? CustomHandler { get; set; }

    /// <summary>
    /// Execute error handling
    /// </summary>
    /// <param name="originalItem">Original item</param>
    /// <param name="exception">Raised exception</param>
    /// <param name="messageContext">Message context</param>
    /// <returns>Whether processing continues (false = skip, true = continue/rethrow)</returns>
    public async Task<bool> HandleErrorAsync<T>(T originalItem, Exception exception, KafkaMessageContext messageContext)
    {
        // Execute custom handler first if configured
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
                return false; // Skip if custom handler throws
            }
        }

        switch (ErrorAction)
        {
            case ErrorAction.Skip:
                // Log the error and skip
                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] SKIP: {exception.Message}");
                return false; // Skip

            case ErrorAction.Retry:
                // Retry logic handled by ProcessItemWithErrorHandling
                // Notify error event after final attempt
                if (ErrorOccurred != null)
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
                    await ErrorOccurred.Invoke(errorContext, messageContext);
                }
                return false; // Skip

            case ErrorAction.DLQ:
                // Notify error event
                if (ErrorOccurred != null)
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
                    await ErrorOccurred.Invoke(errorContext, messageContext);
                }
                return false; // Skip

            default:
                // Skip if action is unknown
                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] UNKNOWN ERROR ACTION: {ErrorAction}, skipping item");
                return false;
        }
    }

}
