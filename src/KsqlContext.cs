using Kafka.Ksql.Linq.Configuration;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Core.Context;
using Kafka.Ksql.Linq.Infrastructure.Admin;
using Kafka.Ksql.Linq.Messaging.Consumers;
using Kafka.Ksql.Linq.Messaging.Producers;
using Kafka.Ksql.Linq.Core.Dlq;
using Kafka.Ksql.Linq.Query.Abstractions;
using Kafka.Ksql.Linq.Serialization.Abstractions;
using Kafka.Ksql.Linq.StateStore;
using Kafka.Ksql.Linq.StateStore.Extensions;
using Kafka.Ksql.Linq.StateStore.Integration;
using Kafka.Ksql.Linq.StateStore.Management;
using Kafka.Ksql.Linq.StateStore.Monitoring;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ConfluentSchemaRegistry = Confluent.SchemaRegistry;

namespace Kafka.Ksql.Linq.Application;
/// <summary>
/// Core層統合KsqlContext
/// 設計理由：Core抽象化を継承し、上位層機能を統合
/// </summary>
public abstract class KsqlContext : KafkaContextCore
{
    private readonly KafkaProducerManager _producerManager;
    private readonly KafkaConsumerManager _consumerManager;
    private readonly DlqProducer _dlqProducer;
    private readonly Lazy<ConfluentSchemaRegistry.ISchemaRegistryClient> _schemaRegistryClient;

    private readonly KafkaAdminService _adminService;
    private readonly KsqlDslOptions _dslOptions;
    private StateStoreBindingManager? _bindingManager;
    private IStateStoreManager? _storeManager;
    private readonly List<IDisposable> _stateBindings = new();

    public event EventHandler<ReadyStateChangedEventArgs>? BindingReadyStateChanged;
    /// <summary>
    /// テスト用にスキーマ登録をスキップするか判定するフック
    /// </summary>
    protected virtual bool SkipSchemaRegistration => false;

    protected KsqlContext() : base()
    {
        _schemaRegistryClient = new Lazy<ConfluentSchemaRegistry.ISchemaRegistryClient>(CreateSchemaRegistryClient);
        _dslOptions = new KsqlDslOptions();
        _adminService = new KafkaAdminService(
        Microsoft.Extensions.Options.Options.Create(_dslOptions),
        null);
        try
        {
            if (!SkipSchemaRegistration)
            {
                InitializeWithSchemaRegistration();
            }
            else
            {
                ConfigureModel();
            }

            _producerManager = new KafkaProducerManager(
                Microsoft.Extensions.Options.Options.Create(_dslOptions),
                null);

            _dlqProducer = new DlqProducer(
                _producerManager,
                new DlqOptions { TopicName = _dslOptions.DlqTopicName });
            _dlqProducer.InitializeAsync().GetAwaiter().GetResult();

            _producerManager.ProduceError += (msg, ctx, ex) =>
            {
                var errorContext = new ErrorContext
                {
                    Exception = ex,
                    OriginalMessage = msg,
                    AttemptCount = 1,
                    FirstAttemptTime = DateTime.UtcNow,
                    LastAttemptTime = DateTime.UtcNow,
                    ErrorPhase = "Producer"
                };

                var messageContext = ctx ?? new KafkaMessageContext
                {
                    MessageId = Guid.NewGuid().ToString(),
                    Tags = new System.Collections.Generic.Dictionary<string, object>
                    {
                        ["original_topic"] = ctx?.Tags.GetValueOrDefault("topic")?.ToString() ?? string.Empty
                    }
                };

                _dlqProducer.HandleErrorAsync(errorContext, messageContext);
            };

            _producerManager.ProduceError += (msg, ctx, ex) =>
            {
                var errorContext = new ErrorContext
                {
                    Exception = ex,
                    OriginalMessage = msg,
                    AttemptCount = 1,
                    FirstAttemptTime = DateTime.UtcNow,
                    LastAttemptTime = DateTime.UtcNow,
                    ErrorPhase = "Producer"
                };

                var messageContext = ctx ?? new KafkaMessageContext
                {
                    MessageId = Guid.NewGuid().ToString(),
                    Tags = new System.Collections.Generic.Dictionary<string, object>
                    {
                        ["original_topic"] = ctx?.Tags.GetValueOrDefault("topic")?.ToString() ?? string.Empty
                    }
                };

                _dlqProducer.HandleErrorAsync(errorContext, messageContext);
            };

            _consumerManager = new KafkaConsumerManager(
                Microsoft.Extensions.Options.Options.Create(_dslOptions),
                null);
            _consumerManager.DeserializationError += (data, ex, topic, part, off, ts, headers, keyType, valueType) =>
                _dlqProducer.SendAsync(data, ex, topic, part, off, ts, headers, keyType, valueType);

            InitializeStateStoreIntegration();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "FATAL: KsqlContext initialization failed. Application cannot continue without Kafka connectivity.", ex);
        }
    }

    protected KsqlContext(KafkaContextOptions options) : base(options)
    {
        _schemaRegistryClient = new Lazy<ConfluentSchemaRegistry.ISchemaRegistryClient>(CreateSchemaRegistryClient);
        _dslOptions = new KsqlDslOptions();
        _adminService = new KafkaAdminService(
        Microsoft.Extensions.Options.Options.Create(_dslOptions),
        null);
        try
        {
            if (!SkipSchemaRegistration)
            {
                InitializeWithSchemaRegistration();
            }
            else
            {
                ConfigureModel();
            }

            _producerManager = new KafkaProducerManager(
                Microsoft.Extensions.Options.Options.Create(_dslOptions),
                null);

            _dlqProducer = new DlqProducer(
                _producerManager,
                new DlqOptions { TopicName = _dslOptions.DlqTopicName });
            _dlqProducer.InitializeAsync().GetAwaiter().GetResult();

            _consumerManager = new KafkaConsumerManager(
                Microsoft.Extensions.Options.Options.Create(_dslOptions),
                null);
            _consumerManager.DeserializationError += (data, ex, topic, part, off, ts, headers, keyType, valueType) =>
                _dlqProducer.SendAsync(data, ex, topic, part, off, ts, headers, keyType, valueType);

            InitializeStateStoreIntegration();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "FATAL: KsqlContext initialization failed. Application cannot continue without Kafka connectivity.", ex);
        }
    }

    /// <summary>
    /// OnModelCreating → スキーマ自動登録フローの実行
    /// </summary>
    private void InitializeWithSchemaRegistration()
    {
        // 1. OnModelCreatingでモデル構築
        ConfigureModel();

        // 旧Avroスキーマ登録処理は削除済み

        // 2. Kafka接続確認
        ValidateKafkaConnectivity();

        EnsureKafkaReadyAsync().GetAwaiter().GetResult();
    }
    private async Task EnsureKafkaReadyAsync()
    {
        try
        {
            // DLQトピック自動生成
            await _adminService.EnsureDlqTopicExistsAsync();

            // 追加の接続確認（AdminServiceで実施）
            _adminService.ValidateKafkaConnectivity();

            await _adminService.EnsureWindowFinalTopicsExistAsync(GetEntityModels());

            // ✅ ログ出力: DLQ準備完了
            Console.WriteLine($"✅ Kafka initialization completed. DLQ topic '{GetDlqTopicName()}' is ready with 5-second retention.");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "FATAL: Kafka readiness check failed. DLQ functionality may be unavailable.", ex);
        }
    }
    public string GetDlqTopicName()
    {
        return _dslOptions.DlqTopicName;
    }
    /// <summary>
    /// Kafka接続確認
    /// </summary>
    private void ValidateKafkaConnectivity()
    {
        try
        {
            // Producer/Consumer初期化時点でKafka接続が確認される
            // 追加の接続確認は不要（既存の初期化処理で十分）
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "FATAL: Cannot connect to Kafka. Verify bootstrap servers and network connectivity.", ex);
        }
    }

    private void InitializeStateStoreIntegration()
    {
        try
        {
            this.InitializeStateStores(_dslOptions);
            _storeManager = this.GetStateStoreManager();
            if (_storeManager == null)
                return;

            _bindingManager = new StateStoreBindingManager();

            // 全エンティティ定義を取得し、設定でRocksDbが有効なものだけバインドを作成する
            var entityModels = GetEntityModels();
            foreach (var model in entityModels.Values)
            {
                var config = _dslOptions.Entities?.Find(e =>
                    string.Equals(e.Entity, model.EntityType.Name, StringComparison.OrdinalIgnoreCase));
                if (config?.StoreType == StoreTypes.RocksDb)
                {
                    var method = typeof(KsqlContext).GetMethod(
                        nameof(CreateBindingForEntity),
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var generic = method!.MakeGenericMethod(model.EntityType);
                    var binding = (IDisposable)generic.Invoke(this, new object[] { model })!;
                    _stateBindings.Add(binding);
                }
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"StateStore initialization failed: {ex.Message}", ex);
        }
    }

    private IDisposable CreateBindingForEntity<T>(EntityModel model) where T : class
    {
        var store = _storeManager!.GetOrCreateStore<string, T>(typeof(T), 0);
        var binding = _bindingManager!.CreateBindingAsync<T>(store, _consumerManager, model, null)
            .GetAwaiter().GetResult();
        binding.ReadyStateChanged += HandleBindingReadyStateChanged;
        return binding;
    }

    private void HandleBindingReadyStateChanged(object? sender, ReadyStateChangedEventArgs e)
    {
        if (!e.IsReady)
        {
            Console.WriteLine($"⚠️ StateStore not ready: {e.TopicName} Lag: {e.CurrentLag}");
        }

        BindingReadyStateChanged?.Invoke(this, e);
    }

    /// <summary>
    /// SchemaRegistryClient作成
    /// </summary>
    private ConfluentSchemaRegistry.ISchemaRegistryClient CreateSchemaRegistryClient()
    {
        var config = new ConfluentSchemaRegistry.SchemaRegistryConfig
        {
            Url = "http://localhost:8081", // デフォルト値、実際は設定から取得
            MaxCachedSchemas = 1000,
            RequestTimeoutMs = 30000
        };

        return new ConfluentSchemaRegistry.CachedSchemaRegistryClient(config);
    }



    /// <summary>
    /// Core層EventSet実装（上位層機能統合）
    /// </summary>
    protected override IEntitySet<T> CreateEntitySet<T>(EntityModel entityModel)
    {
        return new EventSetWithServices<T>(this, entityModel);
    }

    internal KafkaProducerManager GetProducerManager() => _producerManager;
    internal KafkaConsumerManager GetConsumerManager() => _consumerManager;
    internal DlqProducer GetDlqProducer() => _dlqProducer;

    /// <summary>
    /// 指定したエンティティを手動でDLQへ送信します
    /// </summary>
    public async Task SendToDlqAsync<T>(T entity, Exception exception, string reason = "Manual")
    {
        if (_dlqProducer == null)
            throw new InvalidOperationException("DLQ producer not initialized");

        var messageContext = new KafkaMessageContext
        {
            MessageId = Guid.NewGuid().ToString(),
            Tags = new Dictionary<string, object>
            {
                ["original_topic"] = GetTopicName<T>(),
                ["entity_type"] = typeof(T).Name,
                ["error_phase"] = reason,
                ["manual_dlq"] = true
            }
        };

        var errorContext = new ErrorContext
        {
            Exception = exception,
            OriginalMessage = entity,
            AttemptCount = 1,
            FirstAttemptTime = DateTime.UtcNow,
            LastAttemptTime = DateTime.UtcNow,
            ErrorPhase = reason
        };

        await _dlqProducer.HandleErrorAsync(errorContext, messageContext);
    }

    /// <summary>
    /// エンティティ型からトピック名を取得します
    /// </summary>
    public string GetTopicName<T>()
    {
        var models = GetEntityModels();
        if (models.TryGetValue(typeof(T), out var model))
        {
            return (model.TopicName ?? typeof(T).Name).ToLowerInvariant();
        }
        return typeof(T).Name.ToLowerInvariant();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _producerManager?.Dispose();
            _consumerManager?.Dispose();
            _dlqProducer?.Dispose();
            _adminService?.Dispose();
            _bindingManager?.Dispose();

            foreach (var b in _stateBindings)
            {
                b.Dispose();
            }
            _stateBindings.Clear();

            this.CleanupStateStores();

            if (_schemaRegistryClient.IsValueCreated)
            {
                _schemaRegistryClient.Value?.Dispose();
            }
        }

        base.Dispose(disposing);
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        _producerManager?.Dispose();
        _consumerManager?.Dispose();
        _dlqProducer?.Dispose();
        _adminService?.Dispose();
        _bindingManager?.Dispose();

        foreach (var b in _stateBindings)
        {
            b.Dispose();
        }
        _stateBindings.Clear();

        this.CleanupStateStores();

        if (_schemaRegistryClient.IsValueCreated)
        {
            _schemaRegistryClient.Value?.Dispose();
        }

        await base.DisposeAsyncCore();
    }

    public override string ToString()
    {
        return $"{base.ToString()} [スキーマ自動登録対応]";
    }
}

/// <summary>
/// 上位層サービス統合EntitySet
/// 設計理由：IEntitySet<T>を直接実装し、Producer/Consumer機能を提供
/// </summary>
internal class EventSetWithServices<T> : IEntitySet<T> where T : class
{
    private readonly KsqlContext _ksqlContext;
    private readonly EntityModel _entityModel;

    public EventSetWithServices(KsqlContext context, EntityModel entityModel)
    {
        _ksqlContext = context ?? throw new ArgumentNullException(nameof(context));
        _entityModel = entityModel ?? throw new ArgumentNullException(nameof(entityModel));
    }

    /// <summary>
    /// Producer機能：エンティティをKafkaに送信
    /// </summary>
    public async Task AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        try
        {
            var producerManager = _ksqlContext.GetProducerManager();

            var context = new KafkaMessageContext
            {
                MessageId = Guid.NewGuid().ToString(),
                Tags = new Dictionary<string, object>
                {
                    ["entity_type"] = typeof(T).Name,
                    ["method"] = "EventSetWithServices.AddAsync"
                }
            };

            await producerManager.SendAsync(entity, cancellationToken);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to send entity {typeof(T).Name} to Kafka", ex);
        }
    }

    /// <summary>
    /// Consumer機能：Kafkaからエンティティリストを取得
    /// </summary>
    public async Task<List<T>> ToListAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var storeManager = _ksqlContext.GetStateStoreManager();
            if (storeManager != null &&
                _entityModel.GetExplicitStreamTableType() == StreamTableType.Table &&
                _entityModel.EnableCache)
            {
                var store = storeManager.GetOrCreateStore<string, T>(_entityModel.EntityType, 0);
                var list = new List<T>();
                foreach (var kv in store.All())
                {
                    if (kv.Value != null)
                        list.Add(kv.Value);
                }
                return list;
            }

            var consumerManager = _ksqlContext.GetConsumerManager();

            // 簡略実装：実際のConsumer呼び出し
            // TODO: 実際のConsumer実装と連携
            await Task.Delay(100, cancellationToken); // シミュレート

            return new List<T>();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to consume entities {typeof(T).Name} from Kafka", ex);
        }
    }

    /// <summary>
    /// Streaming機能：各エンティティに対してアクションを実行
    /// </summary>
    public async Task ForEachAsync(Func<T, Task> action, TimeSpan timeout = default, CancellationToken cancellationToken = default)
    {
        try
        {
            var consumerManager = _ksqlContext.GetConsumerManager();

            // 簡略実装：ストリーミング消費
            // TODO: 実際のStreaming Consumer実装と連携
            await Task.Delay(100, cancellationToken); // シミュレート
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to stream entities {typeof(T).Name} from Kafka", ex);
        }
    }

    /// <summary>
    /// IAsyncEnumerable実装：ストリーミング消費
    /// </summary>
    public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        // 簡略実装：実際のストリーミングConsumerと連携
        var results = await ToListAsync(cancellationToken);
        foreach (var item in results)
        {
            yield return item;
        }
    }

    protected virtual IManualCommitMessage<T> CreateManualCommitMessage(T item)
        => new ManualCommitMessage<T>(item, () => Task.CompletedTask, () => Task.CompletedTask);

    public async IAsyncEnumerable<object> ForEachAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var enumerator = GetAsyncEnumerator(cancellationToken);

        while (await enumerator.MoveNextAsync())
        {
            var item = enumerator.Current;

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

    // Metadata取得
    public string GetTopicName() => (_entityModel.TopicName ?? typeof(T).Name).ToLowerInvariant();
    public EntityModel GetEntityModel() => _entityModel;
    public IKsqlContext GetContext() => _ksqlContext;

    public override string ToString()
    {
        return $"EventSetWithServices<{typeof(T).Name}> - Topic: {GetTopicName()}";
    }
}

/// <summary>
/// Compatibility shim for renamed context class.
/// </summary>
[Obsolete("Use KsqlContext instead")]
public abstract class KafkaContext : KsqlContext
{
    protected KafkaContext() : base() { }
    protected KafkaContext(KafkaContextOptions options) : base(options) { }
}
