using Confluent.Kafka;
using Kafka.Ksql.Linq.Configuration;
using Kafka.Ksql.Linq.Configuration.Abstractions;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Core.Extensions;
using Kafka.Ksql.Linq.Messaging.Abstractions;
using Kafka.Ksql.Linq.Messaging.Configuration;
using Kafka.Ksql.Linq.Messaging.Consumers.Core;
using Kafka.Ksql.Linq.Serialization;
using Kafka.Ksql.Linq.Serialization.Abstractions;
using Kafka.Ksql.Linq.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ConfluentSchemaRegistry = Confluent.SchemaRegistry;

namespace Kafka.Ksql.Linq.Messaging.Consumers;
/// <summary>
/// 型安全Consumer管理 - Pool削除、直接管理、型安全性強化版
/// 設計理由: EF風API、事前確定管理、型安全性確保
/// </summary>
internal class KafkaConsumerManager : IDisposable
{
    private readonly KsqlDslOptions _options;
    private readonly ILogger? _logger;
    private readonly ILoggerFactory? _loggerFactory;
    private readonly ConcurrentDictionary<Type, object> _consumers = new();
    private readonly ConfluentSerializerFactory _serializerFactory;
    private readonly Lazy<ConfluentSchemaRegistry.ISchemaRegistryClient> _schemaRegistryClient;
    private bool _disposed = false;

    private readonly Action<byte[]?, Exception, string, int, long, DateTime, Headers?, string, string> _sendToDlq;

    public KafkaConsumerManager(
        IOptions<KsqlDslOptions> options,
        Action<byte[]?, Exception, string, int, long, DateTime, Headers?, string, string> sendToDlq,
        ILoggerFactory? loggerFactory = null)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _sendToDlq = sendToDlq ?? throw new ArgumentNullException(nameof(sendToDlq));
        _logger = loggerFactory.CreateLoggerOrNull<KafkaConsumerManager>();
        _loggerFactory = loggerFactory;

        // SchemaRegistryClientの遅延初期化
        _schemaRegistryClient = new Lazy<ConfluentSchemaRegistry.ISchemaRegistryClient>(CreateSchemaRegistryClient);

        _serializerFactory = new ConfluentSerializerFactory(_schemaRegistryClient.Value);

        _logger?.LogInformation("Type-safe KafkaConsumerManager initialized");
    }

    /// <summary>
    /// 型安全Consumer取得 - 事前確定・キャッシュ
    /// </summary>
    public Task<IKafkaConsumer<T, object>> GetConsumerAsync<T>(KafkaSubscriptionOptions? options = null) where T : class
    {
        var entityType = typeof(T);

        if (_consumers.TryGetValue(entityType, out var cachedConsumer))
        {
            return Task.FromResult((IKafkaConsumer<T, object>)cachedConsumer);
        }

        try
        {
            var entityModel = GetEntityModel<T>();
            var topicName = (entityModel.TopicName ?? entityType.Name).ToLowerInvariant();

            // Confluent.Kafka Consumer作成
            var config = BuildConsumerConfig(topicName, options);
            var rawConsumer = new ConsumerBuilder<object, object>(config).Build();

            // Create deserializers via Confluent factory
            var keyType = KeyExtractor.DetermineKeyType(entityModel);
            var keyDeserializer = CreateKeyDeserializer(keyType);
            var valueDeserializer = DeserializerAdapter.Create(_serializerFactory.CreateDeserializer<T>());

            // Build consumer
            var policy = entityModel.DeserializationErrorPolicy == default
                ? _options.DeserializationErrorPolicy
                : entityModel.DeserializationErrorPolicy;

            var consumer = new KafkaConsumer<T, object>(
                rawConsumer,
                keyDeserializer,
                valueDeserializer,
                topicName,
                entityModel,
                policy,
                _options.DlqTopicName,
                _sendToDlq,
                _loggerFactory);

            _consumers.TryAdd(entityType, consumer);

            _logger?.LogDebug("Consumer created: {EntityType} -> {TopicName}", entityType.Name, topicName);
            return Task.FromResult<IKafkaConsumer<T, object>>(consumer);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to create consumer: {EntityType}", entityType.Name);
            throw;
        }
    }

    /// <summary>
    /// エンティティ取得 - EventSetから使用
    /// </summary>
    public async IAsyncEnumerable<T> ConsumeAsync<T>([EnumeratorCancellation] CancellationToken cancellationToken = default) where T : class
    {
        var consumer = await GetConsumerAsync<T>();

        await foreach (var kafkaMessage in consumer.ConsumeAsync(cancellationToken))
        {
            yield return kafkaMessage.Value;
        }
    }

    /// <summary>
    /// エンティティ一覧取得 - EventSetから使用
    /// </summary>
    public async Task<List<T>> FetchAsync<T>(KafkaFetchOptions options, CancellationToken cancellationToken = default) where T : class
    {
        var consumer = await GetConsumerAsync<T>();
        var batchOptions = new KafkaBatchOptions
        {
            MaxBatchSize = options.MaxRecords,
            MaxWaitTime = options.Timeout,
            EnableEmptyBatches = false
        };

        var batch = await consumer.ConsumeBatchAsync(batchOptions, cancellationToken);
        var results = new List<T>();

        foreach (var message in batch.Messages)
        {
            results.Add(message.Value);
        }

        return results;
    }

    /// <summary>
    /// 購読開始
    /// </summary>
    public async Task SubscribeAsync<T>(
        Func<T, KafkaMessageContext, Task> handler,
        KafkaSubscriptionOptions? options = null,
        CancellationToken cancellationToken = default) where T : class
    {
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));

        var consumer = await GetConsumerAsync<T>(options);

        _ = Task.Run(async () =>
        {
            try
            {
                await foreach (var kafkaMessage in consumer.ConsumeAsync(cancellationToken))
                {
                    try
                    {
                        await handler(kafkaMessage.Value, kafkaMessage.Context ?? new KafkaMessageContext());
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Message handler failed: {EntityType}", typeof(T).Name);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger?.LogInformation("Subscription cancelled: {EntityType}", typeof(T).Name);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Subscription error: {EntityType}", typeof(T).Name);
            }
        }, cancellationToken);
    }


    /// <summary>
    /// SchemaRegistryClient作成
    /// </summary>
    private ConfluentSchemaRegistry.ISchemaRegistryClient CreateSchemaRegistryClient()
    {
        var config = new ConfluentSchemaRegistry.SchemaRegistryConfig
        {
            Url = _options.SchemaRegistry.Url,
            MaxCachedSchemas = _options.SchemaRegistry.MaxCachedSchemas,
            RequestTimeoutMs = _options.SchemaRegistry.RequestTimeoutMs
        };

        // Basic認証設定
        if (!string.IsNullOrEmpty(_options.SchemaRegistry.BasicAuthUserInfo))
        {
            config.BasicAuthUserInfo = _options.SchemaRegistry.BasicAuthUserInfo;
            config.BasicAuthCredentialsSource = (ConfluentSchemaRegistry.AuthCredentialsSource)_options.SchemaRegistry.BasicAuthCredentialsSource;
        }

        // SSL設定
        if (!string.IsNullOrEmpty(_options.SchemaRegistry.SslCaLocation))
        {
            config.SslCaLocation = _options.SchemaRegistry.SslCaLocation;
            config.SslKeystoreLocation = _options.SchemaRegistry.SslKeystoreLocation;
            config.SslKeystorePassword = _options.SchemaRegistry.SslKeystorePassword;
        }

        // 追加プロパティ
        foreach (var kvp in _options.SchemaRegistry.AdditionalProperties)
        {
            config.Set(kvp.Key, kvp.Value);
        }

        _logger?.LogDebug("Created SchemaRegistryClient with URL: {Url}", config.Url);
        return new ConfluentSchemaRegistry.CachedSchemaRegistryClient(config);
    }

    /// <summary>
    /// EntityModel作成（簡略実装）
    /// </summary>
    private EntityModel GetEntityModel<T>() where T : class
    {
        var entityType = typeof(T);
        var allProperties = entityType.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        var keyProperties = Array.Empty<PropertyInfo>();

        return new EntityModel
        {
            EntityType = entityType,
            TopicName = entityType.Name.ToLowerInvariant(),
            KeyProperties = keyProperties,
            AllProperties = allProperties
        };
    }

    private IDeserializer<object> CreateKeyDeserializer(Type keyType)
    {
        var method = typeof(IDeserializerFactory).GetMethod("CreateDeserializer")!.MakeGenericMethod(keyType);
        var typed = method.Invoke(_serializerFactory, null);
        var adapterMethod = typeof(DeserializerAdapter).GetMethod("Create")!.MakeGenericMethod(keyType);
        return (IDeserializer<object>)adapterMethod.Invoke(null, new[] { typed! })!;
    }

    /// <summary>
    /// Consumer設定構築
    /// </summary>
    private ConsumerConfig BuildConsumerConfig(string topicName, KafkaSubscriptionOptions? subscriptionOptions)
    {
        var topicConfig = _options.Topics.TryGetValue(topicName, out var config)
            ? config
            : new TopicSection();

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = _options.Common.BootstrapServers,
            ClientId = _options.Common.ClientId,
            GroupId = subscriptionOptions?.GroupId ?? topicConfig.Consumer.GroupId ?? "default-group",
            AutoOffsetReset = Enum.Parse<AutoOffsetReset>(topicConfig.Consumer.AutoOffsetReset),
            EnableAutoCommit = topicConfig.Consumer.EnableAutoCommit,
            AutoCommitIntervalMs = topicConfig.Consumer.AutoCommitIntervalMs,
            SessionTimeoutMs = topicConfig.Consumer.SessionTimeoutMs,
            HeartbeatIntervalMs = topicConfig.Consumer.HeartbeatIntervalMs,
            MaxPollIntervalMs = topicConfig.Consumer.MaxPollIntervalMs,
            FetchMinBytes = topicConfig.Consumer.FetchMinBytes,
            FetchMaxBytes = topicConfig.Consumer.FetchMaxBytes,
            IsolationLevel = Enum.Parse<IsolationLevel>(topicConfig.Consumer.IsolationLevel)
        };

        // 購読オプション適用
        if (subscriptionOptions != null)
        {
            if (subscriptionOptions.AutoCommit.HasValue)
                consumerConfig.EnableAutoCommit = subscriptionOptions.AutoCommit.Value;
            if (subscriptionOptions.SessionTimeout.HasValue)
                consumerConfig.SessionTimeoutMs = (int)subscriptionOptions.SessionTimeout.Value.TotalMilliseconds;
            if (subscriptionOptions.HeartbeatInterval.HasValue)
                consumerConfig.HeartbeatIntervalMs = (int)subscriptionOptions.HeartbeatInterval.Value.TotalMilliseconds;
            if (subscriptionOptions.MaxPollInterval.HasValue)
                consumerConfig.MaxPollIntervalMs = (int)subscriptionOptions.MaxPollInterval.Value.TotalMilliseconds;
        }

        // セキュリティ設定
        if (_options.Common.SecurityProtocol != SecurityProtocol.Plaintext)
        {
            consumerConfig.SecurityProtocol = _options.Common.SecurityProtocol;
            if (_options.Common.SaslMechanism.HasValue)
            {
                consumerConfig.SaslMechanism = _options.Common.SaslMechanism.Value;
                consumerConfig.SaslUsername = _options.Common.SaslUsername;
                consumerConfig.SaslPassword = _options.Common.SaslPassword;
            }

            if (!string.IsNullOrEmpty(_options.Common.SslCaLocation))
            {
                consumerConfig.SslCaLocation = _options.Common.SslCaLocation;
                consumerConfig.SslCertificateLocation = _options.Common.SslCertificateLocation;
                consumerConfig.SslKeyLocation = _options.Common.SslKeyLocation;
                consumerConfig.SslKeyPassword = _options.Common.SslKeyPassword;
            }
        }

        // 追加設定適用
        foreach (var kvp in topicConfig.Consumer.AdditionalProperties)
        {
            consumerConfig.Set(kvp.Key, kvp.Value);
        }

        return consumerConfig;
    }

    /// <summary>
    /// リソース解放
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _logger?.LogInformation("Disposing type-safe KafkaConsumerManager...");

            // Consumerの解放
            foreach (var consumer in _consumers.Values)
            {
                if (consumer is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            _consumers.Clear();

            // SchemaRegistryClientの解放
            if (_schemaRegistryClient.IsValueCreated)
            {
                _schemaRegistryClient.Value?.Dispose();
            }

            _disposed = true;
            _logger?.LogInformation("Type-safe KafkaConsumerManager disposed");
        }
    }
}
