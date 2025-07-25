using Confluent.Kafka;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Core.Extensions;
using Kafka.Ksql.Linq.Messaging.Internal;
using Kafka.Ksql.Linq.Query.Abstractions;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Kafka.Ksql.Linq;

/// <summary>
/// Base class for EventSet implementing IEntitySet<T>
/// Reason for modification: unified with KsqlContext and added IEntitySet<T> implementation
/// </summary>
public abstract class EventSet<T> : IEntitySet<T> where T : class
{
    protected readonly IKsqlContext _context;
    protected readonly EntityModel _entityModel;
    private readonly ErrorHandlingContext _errorHandlingContext;
    private readonly IErrorSink? _dlqErrorSink;

    protected EventSet(IKsqlContext context, EntityModel entityModel, IErrorSink? dlqErrorSink = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _entityModel = entityModel ?? throw new ArgumentNullException(nameof(entityModel));
        _errorHandlingContext = new ErrorHandlingContext();
        _dlqErrorSink = dlqErrorSink;

        if (_dlqErrorSink != null)
        {
            _errorHandlingContext.ErrorOccurred += (ctx, msg) => _dlqErrorSink.HandleErrorAsync(ctx, msg);
        }
    }

    private EventSet(IKsqlContext context, EntityModel entityModel, ErrorHandlingContext errorHandlingContext, IErrorSink? dlqErrorSink)
    {
        _context = context;
        _entityModel = entityModel;
        _errorHandlingContext = errorHandlingContext;
        _dlqErrorSink = dlqErrorSink;

        if (_dlqErrorSink != null)
        {
            _errorHandlingContext.ErrorOccurred += (ctx, msg) => _dlqErrorSink.HandleErrorAsync(ctx, msg);
        }
    }

    /// <summary>
    /// NEW: made abstract - must be implemented by concrete classes
    /// Unifies continuous Kafka consumption and returning a fixed list
    /// </summary>
    public abstract IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default);

    private async IAsyncEnumerable<T> GetAsyncEnumeratorWrapper([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var enumerator = GetAsyncEnumerator(cancellationToken);

        while (true)
        {
            bool hasNext;
            try
            {
                hasNext = await enumerator.MoveNextAsync();
            }
            catch (Exception ex)
            {
                var ctx = new KafkaMessageContext
                {
                    MessageId = Guid.NewGuid().ToString(),
                    Tags = new Dictionary<string, object>
                    {
                        ["processing_phase"] = "ForEachAsync"
                    }
                };

                var shouldContinue = await _errorHandlingContext.HandleErrorAsync(default(T)!, ex, ctx);

                if (!shouldContinue)
                {
                    continue;
                }

                throw;
            }

            if (!hasNext)
                yield break;

            yield return enumerator.Current;
        }
    }


    public virtual async Task<List<T>> ToListAsync(CancellationToken cancellationToken = default)
    {
        if (_entityModel.EntityType == typeof(Core.Models.DlqEnvelope))
            throw new InvalidOperationException("DLQは無限列挙/履歴列であり、バッチ取得・件数指定取得は現状未対応です");

        if (_entityModel.GetExplicitStreamTableType() == StreamTableType.Stream)
            throw new InvalidOperationException("ToListAsync() is not supported on a Stream source. Use ForEachAsync or subscribe for event consumption.");

        var results = new List<T>();

        await foreach (var item in GetAsyncEnumeratorWrapper(cancellationToken))
        {
            results.Add(item);
        }

        return results;
    }
    /// <summary>
    /// ABSTRACT: Producer functionality - implemented in derived classes
    /// </summary>
    protected abstract Task SendEntityAsync(T entity, Dictionary<string, string>? headers, CancellationToken cancellationToken);

    /// <summary>
    /// IEntitySet<T> implementation: producer operations
    /// </summary>
    public virtual async Task AddAsync(T entity, Dictionary<string, string>? headers = null, CancellationToken cancellationToken = default)
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        await SendEntityAsync(entity, headers, cancellationToken);
    }

    public virtual Task RemoveAsync(T entity, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException($"RemoveAsync is not supported for {GetType().Name}.");
    }
    /// <summary>
    /// REDESIGNED: ForEachAsync supporting continuous Kafka consumption
    /// Design change: ToListAsync() is disallowed; now based on GetAsyncEnumerator
    /// </summary>
    public virtual Task ForEachAsync(Func<T, Task> action, TimeSpan timeout = default, CancellationToken cancellationToken = default)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action));

        if (_entityModel.GetExplicitStreamTableType() == StreamTableType.Table)
            throw new InvalidOperationException("ForEachAsync() is not supported on a Table source. Use ToListAsync to obtain the full snapshot.");

        return ForEachAsync((item, ctx) => action(item), timeout, cancellationToken);
    }

    public virtual async Task ForEachAsync(Func<T, KafkaMessage<T,object>, Task> action, TimeSpan timeout = default, CancellationToken cancellationToken = default)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action));

        if (_entityModel.GetExplicitStreamTableType() == StreamTableType.Table)
            throw new InvalidOperationException("ForEachAsync() is not supported on a Table source. Use ToListAsync to obtain the full snapshot.");

        await Task.CompletedTask;

    }

    /// <summary>
    /// IEntitySet<T> implementation: retrieve metadata
    /// </summary>
    public string GetTopicName() => (_entityModel.TopicName ?? _entityModel.EntityType.Name).ToLowerInvariant();

    public EntityModel GetEntityModel() => _entityModel;

    public IKsqlContext GetContext() => _context;

    /// <summary>
    /// Create message context for error handling
    /// </summary>
    private KafkaMessageContext CreateMessageContext(T item)
    {
        return new KafkaMessageContext
        {
            MessageId = Guid.NewGuid().ToString(),
            Tags = new Dictionary<string, object>
            {
                ["entity_type"] = typeof(T).Name,
                ["topic_name"] = GetTopicName(),
                ["processing_phase"] = "ForEachAsync",
                ["timestamp"] = DateTime.UtcNow
            }
        };
    }

    /// <summary>
    /// Creates a ManualCommitMessage instance
    /// </summary>
    protected virtual IManualCommitMessage<T> CreateManualCommitMessage(T item)
    {
        return new ManualCommitMessage<T>(item, () => Task.CompletedTask, () => Task.CompletedTask);
    }

    /// <summary>
    /// Yields messages as different types depending on UseManualCommit
    /// </summary>
    public virtual async IAsyncEnumerable<object> ForEachAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_entityModel.GetExplicitStreamTableType() == StreamTableType.Table)
            throw new InvalidOperationException("ForEachAsync() is not supported on a Table source. Use ToListAsync to obtain the full snapshot.");

        await foreach (var item in GetAsyncEnumeratorWrapper(cancellationToken))
        {
            if (_entityModel.UseManualCommit)
            {
                yield return CreateManualCommitMessage(item);
            }
            else
            {
                yield return item;
            }
        }
    }

    /// <summary>
    /// Configure the error handling policy
    /// </summary>
    internal virtual EventSet<T> WithErrorPolicy(ErrorHandlingPolicy policy)
    {
        // Implement as needed
        return this;
    }

    public override string ToString()
    {
        return $"EventSet<{typeof(T).Name}> - Topic: {GetTopicName()}";
    }



    /// <summary>
    /// Specifies the number of retries.
    /// Used when ErrorAction.Retry is selected.
    /// </summary>
    /// <param name="maxRetries">Maximum retry count</param>
    /// <param name="retryInterval">Retry interval (optional)</param>
    /// <returns>EventSet with retry configuration applied</returns>
    public EventSet<T> WithRetry(int maxRetries, TimeSpan? retryInterval = null)
    {
        if (maxRetries < 0)
            throw new ArgumentException("Retry count must be zero or greater", nameof(maxRetries));

        var newContext = new ErrorHandlingContext
        {
            ErrorAction = _errorHandlingContext.ErrorAction,
            RetryCount = maxRetries,
            RetryInterval = retryInterval ?? TimeSpan.FromSeconds(1)
        };

        return CreateNewInstance(_context, _entityModel, newContext, _dlqErrorSink);
    }

    /// <summary>
    /// Passes the POCO to the business logic.
    /// After receiving from Kafka, each element is transformed using the supplied function.
    /// Exceptions and retries are handled based on the OnError and WithRetry settings.
    /// </summary>
    /// <typeparam name="TResult">Result type</typeparam>
    /// <param name="mapper">Mapping function</param>
    /// <returns>The mapped EventSet</returns>
    public async Task<EventSet<TResult>> Map<TResult>(Func<T, Task<TResult>> mapper) where TResult : class
    {
        if (mapper == null)
            throw new ArgumentNullException(nameof(mapper));

        var results = new List<TResult>();
        var sourceData = await ToListAsync();

        foreach (var item in sourceData)
        {
            var itemErrorContext = new ErrorHandlingContext
            {
                ErrorAction = _errorHandlingContext.ErrorAction,
                RetryCount = _errorHandlingContext.RetryCount,
                RetryInterval = _errorHandlingContext.RetryInterval
            };

            await ProcessItemWithErrorHandling(
                item,
                mapper,
                results,
                itemErrorContext);
        }

        var resultEntityModel = CreateEntityModelForType<TResult>();
        return new MappedEventSet<TResult>(results, _context, resultEntityModel, _dlqErrorSink);
    }

    /// <summary>
    /// Synchronous version of the Map function
    /// </summary>
    public EventSet<TResult> Map<TResult>(Func<T, TResult> mapper) where TResult : class
    {
        if (mapper == null)
            throw new ArgumentNullException(nameof(mapper));

        var results = new List<TResult>();
        var sourceData = ToListAsync().GetAwaiter().GetResult();

        foreach (var item in sourceData)
        {
            var itemErrorContext = new ErrorHandlingContext
            {
                ErrorAction = _errorHandlingContext.ErrorAction,
                RetryCount = _errorHandlingContext.RetryCount,
                RetryInterval = _errorHandlingContext.RetryInterval
            };

            ProcessItemWithErrorHandlingSync(
                item,
                mapper,
                results,
                itemErrorContext);
        }
        var resultEntityModel = CreateEntityModelForType<TResult>();
        return new MappedEventSet<TResult>(results, _context, resultEntityModel, _dlqErrorSink);
    }

    // Abstract method: create a new instance in derived classes
    protected virtual EventSet<T> CreateNewInstance(IKsqlContext context, EntityModel entityModel, ErrorHandlingContext errorContext, IErrorSink? dlqErrorSink)
    {
        // Default implementation: concrete classes must override
        throw new NotImplementedException("Derived classes must implement CreateNewInstance");
    }

    private EntityModel CreateEntityModelForType<TResult>() where TResult : class
    {
        return new EntityModel
        {
            EntityType = typeof(TResult),
            TopicName = $"{typeof(TResult).Name.ToLowerInvariant()}_mapped",
            AllProperties = typeof(TResult).GetProperties(),
            KeyProperties = Array.Empty<System.Reflection.PropertyInfo>(),
            ValidationResult = new ValidationResult { IsValid = true }
        };
    }

    /// <summary>
    /// Item-level processing with error handling (async version)
    /// </summary>
    private async Task ProcessItemWithErrorHandling<TResult>(
        T item,
        Func<T, Task<TResult>> mapper,
        List<TResult> results,
        ErrorHandlingContext errorContext) where TResult : class
    {
        var maxAttempts = errorContext.ErrorAction == ErrorAction.Retry
            ? errorContext.RetryCount + 1
            : 1;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var result = await mapper(item);
                results.Add(result);
                return; // Processing completed successfully
            }
            catch (Exception ex)
            {
                errorContext.CurrentAttempt = attempt;

                // Retry regardless of ErrorAction if this is not the final attempt
                if (attempt < maxAttempts && errorContext.ErrorAction == ErrorAction.Retry)
                {
                    Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Retry {attempt}/{errorContext.RetryCount}: {ex.Message}");
                    await Task.Delay(errorContext.RetryInterval);
                    continue;
                }

                // Perform error handling on the last attempt or when not retrying
                var shouldContinue = await errorContext.HandleErrorAsync(item, ex, CreateContext(item, errorContext));

                if (!shouldContinue)
                {
                    return; // Skip this item and move to the next
                }
            }
        }
    }

    /// <summary>
    /// Item-level processing with error handling (sync version)
    /// </summary>
    private void ProcessItemWithErrorHandlingSync<TResult>(
        T item,
        Func<T, TResult> mapper,
        List<TResult> results,
        ErrorHandlingContext errorContext) where TResult : class
    {
        var maxAttempts = errorContext.ErrorAction == ErrorAction.Retry
            ? errorContext.RetryCount + 1
            : 1;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var result = mapper(item);
                results.Add(result);
                return; // Processing completed successfully
            }
            catch (Exception ex)
            {
                errorContext.CurrentAttempt = attempt;

                // Retry regardless of ErrorAction if this is not the final attempt
                if (attempt < maxAttempts && errorContext.ErrorAction == ErrorAction.Retry)
                {
                    Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Retry {attempt}/{errorContext.RetryCount}: {ex.Message}");
                    Thread.Sleep(errorContext.RetryInterval);
                    continue;
                }

                // Perform error handling on the last attempt or when not retrying
                var shouldContinue = errorContext.HandleErrorAsync(item, ex, CreateContext(item, errorContext)).GetAwaiter().GetResult();

                if (!shouldContinue)
                {
                    return; // Skip this item and proceed to the next
                }
            }
        }
    }

    /// <summary>
    /// Create a message context
    /// </summary>
    private KafkaMessageContext CreateContext(T item, ErrorHandlingContext errorContext)
    {
        return new KafkaMessageContext
        {
            MessageId = Guid.NewGuid().ToString(),
            Tags = new Dictionary<string, object>
            {
                ["original_topic"] = GetTopicName(),
                ["original_partition"] = 0, // Replace with actual value
                ["original_offset"] = 0, // Replace with actual value
                ["retry_count"] = errorContext.CurrentAttempt,
                ["error_phase"] = "Processing"
            }
        };
    }

}
internal class MappedEventSet<T> : EventSet<T> where T : class
{
    private readonly List<T> _mapped;
    private readonly EntityModel _originalEntityModel;

    public MappedEventSet(List<T> mappedItems, IKsqlContext context, EntityModel originalEntityModel, IErrorSink? errorSink = null)
        : base(context, CreateMappedEntityModel<T>(originalEntityModel), errorSink)
    {
        _mapped = mappedItems ?? throw new ArgumentNullException(nameof(mappedItems));
        _originalEntityModel = originalEntityModel;
    }

    /// <summary>
    /// NEW: GetAsyncEnumerator implementation for fixed lists
    /// Returns each _mapped[i] sequentially via yield return
    /// </summary>
    public override async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        foreach (var item in _mapped)
        {
            if (cancellationToken.IsCancellationRequested)
                yield break;

            yield return item;

            // Inserted to treat the loop asynchronously (avoid CPU intensive work)
            await Task.Yield();
        }
    }

    /// <summary>
    /// OPTIMIZATION: ToListAsync - already a fixed list so return immediately
    /// </summary>
    public override async Task<List<T>> ToListAsync(CancellationToken cancellationToken = default)
    {
        if (_entityModel.GetExplicitStreamTableType() == StreamTableType.Stream)
            throw new InvalidOperationException("ToListAsync() is not supported on a Stream source. Use ForEachAsync or subscribe for event consumption.");

        // Already a fixed list; return a copy
        await Task.CompletedTask;
        return new List<T>(_mapped);
    }

    /// <summary>
    /// Data after Map cannot be sent via Producer
    /// </summary>
    protected override Task SendEntityAsync(T entity, Dictionary<string, string>? headers, CancellationToken cancellationToken)
    {
        throw new NotSupportedException(
            $"MappedEventSet<{typeof(T).Name}> does not support AddAsync operations. " +
            "Mapped data is read-only and derived from transformation operations.");
    }

    public override Task RemoveAsync(T entity, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException($"MappedEventSet<{typeof(T).Name}> does not support RemoveAsync operations.");
    }

    /// <summary>
    /// Helper method to create a MappedEventSet
    /// </summary>
    public static MappedEventSet<T> Create(List<T> mappedItems, IKsqlContext context, EntityModel originalEntityModel, IErrorSink? errorSink = null)
    {
        return new MappedEventSet<T>(mappedItems, context, originalEntityModel, errorSink);
    }

    /// <summary>
    /// Create a MappedEventSet with DLQ support
    /// </summary>
    public static MappedEventSet<T> CreateWithDlq(List<T> mappedItems, IKsqlContext context, EntityModel originalEntityModel, IErrorSink dlqErrorSink)
    {
        return new MappedEventSet<T>(mappedItems, context, originalEntityModel, dlqErrorSink);
    }

    /// <summary>
    /// Create an EntityModel for mapped data
    /// </summary>
    private static EntityModel CreateMappedEntityModel<TMapped>(EntityModel originalModel) where TMapped : class
    {
        return new EntityModel
        {
            EntityType = typeof(TMapped),
            TopicName = $"{originalModel.GetTopicName()}_mapped",
            AllProperties = typeof(TMapped).GetProperties(),
            KeyProperties = Array.Empty<System.Reflection.PropertyInfo>(), // No key after mapping
            ValidationResult = new ValidationResult { IsValid = true }
        };
    }

    public override string ToString()
    {
        return $"MappedEventSet<{typeof(T).Name}> - Items: {_mapped.Count}";
    }
}
