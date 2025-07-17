using Confluent.Kafka;
using Kafka.Ksql.Linq.Configuration;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Core.Extensions;
using Kafka.Ksql.Linq.Messaging.Abstractions;
using Kafka.Ksql.Linq.Messaging.Configuration;
using Kafka.Ksql.Linq.Messaging.Producers.Core;
using Confluent.SchemaRegistry.Serdes;
using Confluent.Kafka.SyncOverAsync;
using Kafka.Ksql.Linq.Messaging.Internal;
using Kafka.Ksql.Linq.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using ConfluentSchemaRegistry = Confluent.SchemaRegistry;
/// <summary>
/// 型安全Producer管理 - Pool削除、直接管理、型安全性強化版
/// 設計理由: EF風API、事前確定管理、型安全性確保
/// </summary>
internal class KafkaProducerManager : IDisposable
{
    private readonly KsqlDslOptions _options;
    private readonly ILogger? _logger;
    private readonly ILoggerFactory? _loggerFactory;
    private readonly ConcurrentDictionary<Type, object> _producers = new();
    private readonly ConcurrentDictionary<(Type, string), object> _topicProducers = new();
    private readonly ConcurrentDictionary<Type, ISerializer<object>> _keySerializerCache = new();
    private readonly ConcurrentDictionary<Type, ISerializer<object>> _valueSerializerCache = new();
    private readonly AvroSerializerConfig _serializerConfig = new();
    private readonly Lazy<ConfluentSchemaRegistry.ISchemaRegistryClient> _schemaRegistryClient;
    private bool _disposed = false;


    public KafkaProducerManager(
        IOptions<KsqlDslOptions> options,
        ILoggerFactory? loggerFactory = null)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = loggerFactory.CreateLoggerOrNull<KafkaProducerManager>();
        _loggerFactory = loggerFactory;

        // SchemaRegistryClientの遅延初期化
        _schemaRegistryClient = new Lazy<ConfluentSchemaRegistry.ISchemaRegistryClient>(CreateSchemaRegistryClient);

        _logger?.LogInformation("Type-safe KafkaProducerManager initialized");
    }

    /// <summary>
    /// 型安全Producer取得 - 事前確定・キャッシュ
    /// </summary>
    public Task<IKafkaProducer<T>> GetProducerAsync<T>() where T : class
    {
        var entityType = typeof(T);

        if (_producers.TryGetValue(entityType, out var cachedProducer))
        {
            return Task.FromResult((IKafkaProducer<T>)cachedProducer);
        }

        try
        {
            var entityModel = GetEntityModel<T>();
            var topicName = (entityModel.TopicName ?? entityType.Name).ToLowerInvariant();

            // Confluent.Kafka Producer作成
            var config = BuildProducerConfig(topicName);
            var rawProducer = new ProducerBuilder<object, object>(config).Build();

            // Serializer creation via Confluent factory
            var keyType = KeyExtractor.DetermineKeyType(entityModel);
            var keySerializer = CreateKeySerializer(keyType);

            var valueSerializer = GetValueSerializer<T>();

            var producer = new KafkaProducer<T>(
                rawProducer,
                keySerializer,
                valueSerializer,
                topicName,
                entityModel,
                _loggerFactory);


            _producers.TryAdd(entityType, producer);

            _logger?.LogDebug("Producer created: {EntityType} -> {TopicName}", entityType.Name, topicName);
            return Task.FromResult<IKafkaProducer<T>>(producer);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to create producer: {EntityType}", entityType.Name);
            throw;
        }
    }

    private Task<IKafkaProducer<T>> GetProducerAsync<T>(string topicName) where T : class
    {
        var key = (typeof(T), topicName);
        if (_topicProducers.TryGetValue(key, out var existing))
        {
            return Task.FromResult((IKafkaProducer<T>)existing);
        }

        var entityModel = GetEntityModel<T>();

        var config = BuildProducerConfig(topicName);
        var rawProducer = new ProducerBuilder<object, object>(config).Build();

        var keyType = KeyExtractor.DetermineKeyType(entityModel);
        var keySerializer = CreateKeySerializer(keyType);

        var valueSerializer = GetValueSerializer<T>();

        var producer = new KafkaProducer<T>(
            rawProducer,
            keySerializer,
            valueSerializer,
            topicName,
            entityModel,
            _loggerFactory);


        _topicProducers.TryAdd(key, producer);
        return Task.FromResult<IKafkaProducer<T>>(producer);
    }
    /// <summary>
    /// Producer設定構築
    /// </summary>
    private ProducerConfig BuildProducerConfig(string topicName)
    {
        var topicConfig = _options.Topics.TryGetValue(topicName, out var config)
            ? config
            : new TopicSection();

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = _options.Common.BootstrapServers,
            ClientId = _options.Common.ClientId,
            Acks = Enum.Parse<Acks>(topicConfig.Producer.Acks),
            CompressionType = Enum.Parse<CompressionType>(topicConfig.Producer.CompressionType),
            EnableIdempotence = topicConfig.Producer.EnableIdempotence,
            MaxInFlight = topicConfig.Producer.MaxInFlightRequestsPerConnection,
            LingerMs = topicConfig.Producer.LingerMs,
            BatchSize = topicConfig.Producer.BatchSize,
            RetryBackoffMs = topicConfig.Producer.RetryBackoffMs
        };

        // パーティショナー設定
        if (!string.IsNullOrEmpty(topicConfig.Producer.Partitioner))
        {
            // パーティショナーは文字列またはクラス名として設定
            producerConfig.Set("partitioner.class", topicConfig.Producer.Partitioner);
        }

        // セキュリティ設定
        if (_options.Common.SecurityProtocol != SecurityProtocol.Plaintext)
        {
            producerConfig.SecurityProtocol = _options.Common.SecurityProtocol;
            if (_options.Common.SaslMechanism.HasValue)
            {
                producerConfig.SaslMechanism = _options.Common.SaslMechanism.Value;
                producerConfig.SaslUsername = _options.Common.SaslUsername;
                producerConfig.SaslPassword = _options.Common.SaslPassword;
            }

            if (!string.IsNullOrEmpty(_options.Common.SslCaLocation))
            {
                producerConfig.SslCaLocation = _options.Common.SslCaLocation;
                producerConfig.SslCertificateLocation = _options.Common.SslCertificateLocation;
                producerConfig.SslKeyLocation = _options.Common.SslKeyLocation;
                producerConfig.SslKeyPassword = _options.Common.SslKeyPassword;
            }
        }

        // 追加設定適用
        foreach (var kvp in topicConfig.Producer.AdditionalProperties)
        {
            producerConfig.Set(kvp.Key, kvp.Value);
        }

        return producerConfig;
    }

    // ✅ 追加：SchemaRegistryClient作成（ConsumerManagerと同様）
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

        // SSL設定 - テスト環境で実在しないパスが指定されても例外にならないよう存在確認を行う
        if (!string.IsNullOrEmpty(_options.SchemaRegistry.SslCaLocation)
            && File.Exists(_options.SchemaRegistry.SslCaLocation))
        {
            config.SslCaLocation = _options.SchemaRegistry.SslCaLocation;

            if (!string.IsNullOrEmpty(_options.SchemaRegistry.SslKeystoreLocation)
                && File.Exists(_options.SchemaRegistry.SslKeystoreLocation))
            {
                config.SslKeystoreLocation = _options.SchemaRegistry.SslKeystoreLocation;

                if (!string.IsNullOrEmpty(_options.SchemaRegistry.SslKeystorePassword))
                {
                    config.SslKeystorePassword = _options.SchemaRegistry.SslKeystorePassword;
                }
            }
        }

        // 追加プロパティ
        foreach (var kvp in _options.SchemaRegistry.AdditionalProperties)
        {
            config.Set(kvp.Key, kvp.Value);
        }

        // SslKeyPasswordをAdditionalPropertyとして設定
        if (!string.IsNullOrEmpty(_options.SchemaRegistry.SslKeyPassword))
        {
            config.Set("ssl.key.password", _options.SchemaRegistry.SslKeyPassword);
        }

        _logger?.LogDebug("Created SchemaRegistryClient with URL: {Url}", config.Url);
        return new ConfluentSchemaRegistry.CachedSchemaRegistryClient(config);
    }

    // ✅ 追加：EntityModel作成（ConsumerManagerと同様）
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

    private ISerializer<object> CreateKeySerializer(Type keyType)
    {
        if (_keySerializerCache.TryGetValue(keyType, out var cached))
            return cached;

        var method = typeof(KafkaProducerManager).GetMethod(nameof(CreateKeySerializerGeneric), BindingFlags.NonPublic | BindingFlags.Instance)!
            .MakeGenericMethod(keyType);
        var serializer = (ISerializer<object>)method.Invoke(this, null)!;
        _keySerializerCache[keyType] = serializer;
        return serializer;
    }

    private ISerializer<object> CreateKeySerializerGeneric<T>()
    {
        var typed = new AvroSerializer<T>(_schemaRegistryClient.Value, _serializerConfig).AsSyncOverAsync();
        return SerializerAdapters.ToObjectSerializer(typed);
    }

    private ISerializer<object> GetValueSerializer<T>()
    {
        var type = typeof(T);
        if (_valueSerializerCache.TryGetValue(type, out var cached))
            return cached;

        var typed = new AvroSerializer<T>(_schemaRegistryClient.Value, _serializerConfig).AsSyncOverAsync();
        var serializer = SerializerAdapters.ToObjectSerializer(typed);
        _valueSerializerCache[type] = serializer;
        return serializer;
    }
    public async Task SendAsync<T>(T entity, CancellationToken cancellationToken = default) where T : class
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        var producer = await GetProducerAsync<T>();
        var context = new KafkaMessageContext
        {
            MessageId = Guid.NewGuid().ToString(),
            Tags = new Dictionary<string, object>
            {
                ["entity_type"] = typeof(T).Name,
                ["method"] = "SendAsync"
            }
        };

        await producer.SendAsync(entity, context, cancellationToken);
    }

    public async Task SendAsync<T>(string topicName, T entity, CancellationToken cancellationToken = default) where T : class
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        var producer = await GetProducerAsync<T>(topicName);
        var context = new KafkaMessageContext
        {
            MessageId = Guid.NewGuid().ToString(),
            Tags = new Dictionary<string, object>
            {
                ["entity_type"] = typeof(T).Name,
                ["method"] = "SendAsync"
            }
        };

        await producer.SendAsync(entity, context, cancellationToken);
    }
    // 既存のメソッドは変更なし（SendAsync, SendRangeAsync, BuildProducerConfig）

    // ✅ 修正：Disposeメソッドの更新
    public void Dispose()
    {
        if (!_disposed)
        {
            _logger?.LogInformation("Disposing type-safe KafkaProducerManager...");

            // Producerの解放
            foreach (var producer in _producers.Values)
            {
                if (producer is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            _producers.Clear();

            foreach (var producer in _topicProducers.Values)
            {
                if (producer is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            _topicProducers.Clear();

            _keySerializerCache.Clear();
            _valueSerializerCache.Clear();



            // ✅ 追加：SchemaRegistryClientの解放
            if (_schemaRegistryClient.IsValueCreated)
            {
                _schemaRegistryClient.Value?.Dispose();
            }

            _disposed = true;
            _logger?.LogInformation("Type-safe KafkaProducerManager disposed");
        }
    }
}