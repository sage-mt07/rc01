using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Core.Extensions;
using Kafka.Ksql.Linq.Messaging.Contracts;
using Kafka.Ksql.Linq.Messaging.Internal;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Kafka.Ksql.Linq;

/// <summary>
/// EventSet基底クラス - IEntitySet<T>を実装
/// 修正理由: KsqlContextとの統合、IEntitySet<T>実装追加
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
    }

    private EventSet(IKsqlContext context, EntityModel entityModel, ErrorHandlingContext errorHandlingContext, IErrorSink? dlqErrorSink)
    {
        _context = context;
        _entityModel = entityModel;
        _errorHandlingContext = errorHandlingContext;
        _dlqErrorSink = dlqErrorSink;
    }

    /// <summary>
    /// ✅ NEW: 抽象メソッド化 - 具象クラスで実装必須
    /// Kafka連続受信 or 固定リスト返しを統一
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
        var results = new List<T>();

        await foreach (var item in GetAsyncEnumeratorWrapper(cancellationToken))
        {
            results.Add(item);
        }

        return results;
    }
    /// <summary>
    /// ✅ ABSTRACT: Producer機能 - 具象クラスで実装
    /// </summary>
    protected abstract Task SendEntityAsync(T entity, CancellationToken cancellationToken);

    /// <summary>
    /// IEntitySet<T> 実装: Producer操作
    /// </summary>
    public virtual async Task AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        await SendEntityAsync(entity, cancellationToken);
    }
    /// <summary>
    /// ✅ REDESIGNED: Kafka継続受信対応ForEachAsync
    /// 設計変更: ToListAsync()使用禁止 → GetAsyncEnumeratorベース
    /// </summary>
    public virtual async Task ForEachAsync(Func<T, Task> action, TimeSpan timeout = default, CancellationToken cancellationToken = default)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action));

        var start = DateTime.UtcNow;
        var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            await foreach (var item in GetAsyncEnumeratorWrapper(combinedCts.Token))
            {
                // タイムアウトチェック（継続受信処理の最大継続時間）
                if (timeout != default && DateTime.UtcNow - start > timeout)
                {
                    break;
                }

                // エラーハンドリング付きアクション実行
                try
                {
                    await action(item);
                }
                catch (Exception ex)
                {
                    var messageContext = CreateMessageContext(item);
                    var shouldContinue = await _errorHandlingContext.HandleErrorAsync(item, ex, messageContext);

                    if (!shouldContinue)
                    {
                        continue; // エラー時スキップして次のアイテムへ
                    }

                    // shouldContinue=true の場合は例外を再スロー
                    throw;
                }
            }
        }
        finally
        {
            combinedCts?.Dispose();
        }
    }

    /// <summary>
    /// IEntitySet<T> 実装: メタデータ取得
    /// </summary>
    public string GetTopicName() => (_entityModel.TopicName ?? _entityModel.EntityType.Name).ToLowerInvariant();

    public EntityModel GetEntityModel() => _entityModel;

    public IKsqlContext GetContext() => _context;

    /// <summary>
    /// エラーハンドリング用メッセージコンテキスト作成
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
    /// ManualCommitMessage インスタンスを作成します
    /// </summary>
    protected virtual IManualCommitMessage<T> CreateManualCommitMessage(T item)
    {
        return new ManualCommitMessage<T>(item, () => Task.CompletedTask, () => Task.CompletedTask);
    }

    /// <summary>
    /// UseManualCommit に応じて型を切り替えてメッセージをyieldします
    /// </summary>
    public virtual async IAsyncEnumerable<object> ForEachAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
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
    /// エラーハンドリングポリシー設定
    /// </summary>
    internal virtual EventSet<T> WithErrorPolicy(ErrorHandlingPolicy policy)
    {
        // 必要に応じて実装
        return this;
    }

    public override string ToString()
    {
        return $"EventSet<{typeof(T).Name}> - Topic: {GetTopicName()}";
    }



    /// <summary>
    /// リトライ回数を指定します
    /// ErrorAction.Retry指定時に使用されます
    /// </summary>
    /// <param name="maxRetries">最大リトライ回数</param>
    /// <param name="retryInterval">リトライ間隔（オプション）</param>
    /// <returns>リトライ設定が追加されたEventSet</returns>
    public EventSet<T> WithRetry(int maxRetries, TimeSpan? retryInterval = null)
    {
        if (maxRetries < 0)
            throw new ArgumentException("リトライ回数は0以上である必要があります", nameof(maxRetries));

        var newContext = new ErrorHandlingContext
        {
            ErrorAction = _errorHandlingContext.ErrorAction,
            RetryCount = maxRetries,
            RetryInterval = retryInterval ?? TimeSpan.FromSeconds(1),
            ErrorSink = _errorHandlingContext.ErrorSink
        };

        return CreateNewInstance(_context, _entityModel, newContext, _dlqErrorSink);
    }

    /// <summary>
    /// POCOを業務ロジックに渡します
    /// Kafkaから受信後、指定された関数で各要素を変換します
    /// OnErrorとWithRetryの設定に基づいて例外処理とリトライを実行します
    /// </summary>
    /// <typeparam name="TResult">変換後の型</typeparam>
    /// <param name="mapper">変換関数</param>
    /// <returns>変換されたEventSet</returns>
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
                RetryInterval = _errorHandlingContext.RetryInterval,
                ErrorSink = _errorHandlingContext.ErrorSink
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
    /// 同期版のMap関数
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
                RetryInterval = _errorHandlingContext.RetryInterval,
                ErrorSink = _errorHandlingContext.ErrorSink
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

    // ✅ 抽象メソッド：派生クラスで新しいインスタンス作成
    protected virtual EventSet<T> CreateNewInstance(IKsqlContext context, EntityModel entityModel, ErrorHandlingContext errorContext, IErrorSink? dlqErrorSink)
    {
        // デフォルト実装：具象クラスでオーバーライド必要
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
    /// アイテム単位のエラーハンドリング付き処理（非同期版）
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
                return; // 成功時は処理完了
            }
            catch (Exception ex)
            {
                errorContext.CurrentAttempt = attempt;

                // 最後の試行でない場合、ErrorActionに関係なくリトライ
                if (attempt < maxAttempts && errorContext.ErrorAction == ErrorAction.Retry)
                {
                    Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] リトライ {attempt}/{errorContext.RetryCount}: {ex.Message}");
                    await Task.Delay(errorContext.RetryInterval);
                    continue;
                }

                // 最後の試行または非リトライの場合、エラーハンドリング実行
                var shouldContinue = await errorContext.HandleErrorAsync(item, ex, CreateContext(item, errorContext));

                if (!shouldContinue)
                {
                    return; // アイテムをスキップして次へ
                }
            }
        }
    }

    /// <summary>
    /// アイテム単位のエラーハンドリング付き処理（同期版）
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
                return; // 成功時は処理完了
            }
            catch (Exception ex)
            {
                errorContext.CurrentAttempt = attempt;

                // 最後の試行でない場合、ErrorActionに関係なくリトライ
                if (attempt < maxAttempts && errorContext.ErrorAction == ErrorAction.Retry)
                {
                    Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] リトライ {attempt}/{errorContext.RetryCount}: {ex.Message}");
                    Thread.Sleep(errorContext.RetryInterval);
                    continue;
                }

                // 最後の試行または非リトライの場合、エラーハンドリング実行
                var shouldContinue = errorContext.HandleErrorAsync(item, ex, CreateContext(item, errorContext)).GetAwaiter().GetResult();

                if (!shouldContinue)
                {
                    return; // アイテムをスキップして次へ
                }
            }
        }
    }

    /// <summary>
    /// メッセージコンテキストを作成
    /// </summary>
    private KafkaMessageContext CreateContext(T item, ErrorHandlingContext errorContext)
    {
        return new KafkaMessageContext
        {
            MessageId = Guid.NewGuid().ToString(),
            Tags = new Dictionary<string, object>
            {
                ["original_topic"] = GetTopicName(),
                ["original_partition"] = 0, // 実際の値に置き換え
                ["original_offset"] = 0, // 実際の値に置き換え
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
    /// ✅ NEW: 固定リスト用GetAsyncEnumerator実装
    /// yield return _mapped[i] で順次返却
    /// </summary>
    public override async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        foreach (var item in _mapped)
        {
            if (cancellationToken.IsCancellationRequested)
                yield break;

            yield return item;

            // 非同期として扱うために挿入（CPU集約的な処理を避ける）
            await Task.Yield();
        }
    }

    /// <summary>
    /// ✅ 最適化: ToListAsync - 既に固定リストなので即座に返却
    /// </summary>
    public override async Task<List<T>> ToListAsync(CancellationToken cancellationToken = default)
    {
        // 既に固定リストなので、コピーして返却
        await Task.CompletedTask;
        return new List<T>(_mapped);
    }

    /// <summary>
    /// Map後のデータはProducer送信不可
    /// </summary>
    protected override Task SendEntityAsync(T entity, CancellationToken cancellationToken)
    {
        throw new NotSupportedException(
            $"MappedEventSet<{typeof(T).Name}> does not support AddAsync operations. " +
            "Mapped data is read-only and derived from transformation operations.");
    }

    /// <summary>
    /// MappedEventSet作成用ヘルパーメソッド
    /// </summary>
    public static MappedEventSet<T> Create(List<T> mappedItems, IKsqlContext context, EntityModel originalEntityModel, IErrorSink? errorSink = null)
    {
        return new MappedEventSet<T>(mappedItems, context, originalEntityModel, errorSink);
    }

    /// <summary>
    /// DLQ対応MappedEventSet作成
    /// </summary>
    public static MappedEventSet<T> CreateWithDlq(List<T> mappedItems, IKsqlContext context, EntityModel originalEntityModel, IErrorSink dlqErrorSink)
    {
        return new MappedEventSet<T>(mappedItems, context, originalEntityModel, dlqErrorSink);
    }

    /// <summary>
    /// Mapped用のEntityModel作成
    /// </summary>
    private static EntityModel CreateMappedEntityModel<TMapped>(EntityModel originalModel) where TMapped : class
    {
        return new EntityModel
        {
            EntityType = typeof(TMapped),
            TopicName = $"{originalModel.GetTopicName()}_mapped",
            AllProperties = typeof(TMapped).GetProperties(),
            KeyProperties = Array.Empty<System.Reflection.PropertyInfo>(), // Map後はキーなし
            ValidationResult = new ValidationResult { IsValid = true }
        };
    }

    public override string ToString()
    {
        return $"MappedEventSet<{typeof(T).Name}> - Items: {_mapped.Count}";
    }
}
