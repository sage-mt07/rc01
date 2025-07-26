using Confluent.Kafka;
using Kafka.Ksql.Linq.Cache.Core;
using Kafka.Ksql.Linq.Cache.Extensions;
using Kafka.Ksql.Linq.Configuration;
using Kafka.Ksql.Linq.Configuration.Abstractions;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Core.Dlq;
using Kafka.Ksql.Linq.Core.Extensions;
using Kafka.Ksql.Linq.Core.Modeling;
using Kafka.Ksql.Linq.Infrastructure.Admin;
using Kafka.Ksql.Linq.Mapping;
using Kafka.Ksql.Linq.Messaging.Consumers;
using Kafka.Ksql.Linq.Query.Abstractions;
using Kafka.Ksql.Linq.Query.Ddl;
using Kafka.Ksql.Linq.Application;
using Kafka.Ksql.Linq.SchemaRegistryTools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ConfluentSchemaRegistry = Confluent.SchemaRegistry;

namespace Kafka.Ksql.Linq;
/// <summary>
/// KsqlContext that integrates the Core layer.
/// Design rationale: inherits core abstractions and integrates higher-level features.
/// </summary>
public abstract class KsqlContext : IKsqlContext
{
    private readonly KafkaProducerManager _producerManager;
    private readonly Dictionary<Type, EntityModel> _entityModels = new();
    private readonly Dictionary<Type, object> _entitySets = new();
    private bool _disposed = false;
    private readonly KafkaConsumerManager _consumerManager;
    private readonly DlqProducer _dlqProducer;
    private readonly Lazy<ConfluentSchemaRegistry.ISchemaRegistryClient> _schemaRegistryClient;
    private readonly Lazy<HttpClient> _ksqlDbClient;

    private readonly KafkaAdminService _adminService;
    private readonly KsqlDslOptions _dslOptions;
    private TableCacheRegistry? _cacheRegistry;
    private readonly MappingRegistry _mappingRegistry = new();
    private readonly ILogger _logger;
    private readonly ILoggerFactory? _loggerFactory;

    internal ILogger Logger => _logger; 



    /// <summary>
    /// Hook to decide whether schema registration should be skipped for tests
    /// </summary>
    protected virtual bool SkipSchemaRegistration => false;

    public const string DefaultSectionName = "KsqlDsl";

    protected KsqlContext(IConfiguration configuration,ILoggerFactory? loggerFactory=null)
        : this(configuration, DefaultSectionName,loggerFactory)
    {
    }

    protected KsqlContext(IConfiguration configuration, string sectionName,ILoggerFactory? loggerFactory=null)
    {
        _schemaRegistryClient = new Lazy<ConfluentSchemaRegistry.ISchemaRegistryClient>(CreateSchemaRegistryClient);
        _ksqlDbClient = new Lazy<HttpClient>(CreateClient);
        _dslOptions = new KsqlDslOptions();
        configuration.GetSection(sectionName).Bind(_dslOptions);
        DecimalPrecisionConfig.DecimalPrecision = _dslOptions.DecimalPrecision;
        DecimalPrecisionConfig.DecimalScale = _dslOptions.DecimalScale;
        _loggerFactory = loggerFactory ?? configuration.CreateLoggerFactory();
        _logger = _loggerFactory.CreateLoggerOrNull<KsqlContext>();

        _adminService = new KafkaAdminService(
        Microsoft.Extensions.Options.Options.Create(_dslOptions),
        _loggerFactory);
        InitializeEntityModels();
        try
        {
            _producerManager = new KafkaProducerManager(
                Microsoft.Extensions.Options.Options.Create(_dslOptions),
                _loggerFactory);

            if (!SkipSchemaRegistration)
            {
                InitializeWithSchemaRegistration();
            }
            else
            {
                ConfigureModel();
            }



            _dlqProducer = new DlqProducer(
                _producerManager,
                _dslOptions.DlqOptions);
            _dlqProducer.InitializeAsync().GetAwaiter().GetResult();

            _consumerManager = new KafkaConsumerManager(
                Microsoft.Extensions.Options.Options.Create(_dslOptions),
                _loggerFactory);
            _consumerManager.DeserializationError += (data, ex, topic, part, off, ts, headers, keyType, valueType) =>
                _dlqProducer.SendAsync(data, ex, topic, part, off, ts, headers, keyType, valueType);

            this.UseTableCache(_dslOptions, _loggerFactory);
            _cacheRegistry = this.GetTableCacheRegistry();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"KsqlContext initialization failed: {ex.Message} (section: {sectionName})");
            throw;
        }
    }

    protected KsqlContext(KsqlDslOptions options,ILoggerFactory? loggerFactory=null)
    {
        _schemaRegistryClient = new Lazy<ConfluentSchemaRegistry.ISchemaRegistryClient>(CreateSchemaRegistryClient);
        _ksqlDbClient = new Lazy<HttpClient>(CreateClient);
        _dslOptions = options;
        DecimalPrecisionConfig.DecimalPrecision = _dslOptions.DecimalPrecision;
        DecimalPrecisionConfig.DecimalScale = _dslOptions.DecimalScale;

        _loggerFactory = loggerFactory;
        _logger = _loggerFactory.CreateLoggerOrNull<KsqlContext>();


        _adminService = new KafkaAdminService(
        Microsoft.Extensions.Options.Options.Create(_dslOptions),
        _loggerFactory);
        InitializeEntityModels();
        try
        {
            _producerManager = new KafkaProducerManager(
                 Microsoft.Extensions.Options.Options.Create(_dslOptions),
                 _loggerFactory);
            if (!SkipSchemaRegistration)
            {
                InitializeWithSchemaRegistration();
            }
            else
            {
                ConfigureModel();
            }

 

            _dlqProducer = new DlqProducer(
                _producerManager,
                _dslOptions.DlqOptions);
            _dlqProducer.InitializeAsync().GetAwaiter().GetResult();

            _consumerManager = new KafkaConsumerManager(
                Microsoft.Extensions.Options.Options.Create(_dslOptions),
                _loggerFactory);
            _consumerManager.DeserializationError += (data, ex, topic, part, off, ts, headers, keyType, valueType) =>
                _dlqProducer.SendAsync(data, ex, topic, part, off, ts, headers, keyType, valueType);

            this.UseTableCache(_dslOptions, _loggerFactory);
        _cacheRegistry = this.GetTableCacheRegistry();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"KsqlContext initialization failed: {ex.Message} ");
            throw;
        }
    }

    protected virtual void OnModelCreating(IModelBuilder modelBuilder) { }

    public IEntitySet<T> Set<T>() where T : class
    {
        var entityType = typeof(T);

        if (entityType == typeof(Core.Models.DlqEnvelope))
        {
            return (IEntitySet<T>)GetDlqStream();
        }

        if (_entitySets.TryGetValue(entityType, out var existingSet))
        {
            return (IEntitySet<T>)existingSet;
        }

        var entityModel = GetOrCreateEntityModel<T>();
        var entitySet = CreateEntitySet<T>(entityModel);
        _entitySets[entityType] = entitySet;

        return entitySet;
    }

    public object GetEventSet(Type entityType)
    {
        if (_entitySets.TryGetValue(entityType, out var entitySet))
        {
            return entitySet;
        }

        var entityModel = GetOrCreateEntityModel(entityType);
        var createdSet = CreateEntitySet(entityType, entityModel);
        _entitySets[entityType] = createdSet;

        return createdSet;
    }

    public Dictionary<Type, EntityModel> GetEntityModels()
    {
        return new Dictionary<Type, EntityModel>(_entityModels);
    }

    protected virtual object CreateEntitySet(Type entityType, EntityModel entityModel)
    {
        var method = GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(m =>
                m.Name == nameof(CreateEntitySet)
                && m.IsGenericMethodDefinition
                && m.GetGenericArguments().Length == 1
                && m.GetParameters().Length == 1
                && m.GetParameters()[0].ParameterType == typeof(EntityModel)
            );

        if (method == null)
            throw new InvalidOperationException("Generic CreateEntitySet<T>(EntityModel) not found!");

        // このあと
        var genericMethod = method.MakeGenericMethod(entityType);
        return genericMethod.Invoke(this, new object[] { entityModel })!;
    }

    protected void ConfigureModel()
    {
        var modelBuilder = new ModelBuilder(_dslOptions.ValidationMode);
        using (Kafka.Ksql.Linq.Core.Modeling.ModelCreatingScope.Enter())
        {
            OnModelCreating(modelBuilder);
        }
        ApplyModelBuilderSettings(modelBuilder);
    }

    private void InitializeEntityModels()
    {
        var dlqModel = CreateEntityModelFromType(typeof(Core.Models.DlqEnvelope));
        dlqModel.SetStreamTableType(Query.Abstractions.StreamTableType.Stream);
        dlqModel.TopicName = GetDlqTopicName();
        dlqModel.AccessMode = Core.Abstractions.EntityAccessMode.ReadOnly;
        _entityModels[typeof(Core.Models.DlqEnvelope)] = dlqModel;
        _mappingRegistry.RegisterEntityModel(dlqModel);
    }

    private void ApplyModelBuilderSettings(ModelBuilder modelBuilder)
    {
        var models = modelBuilder.GetAllEntityModels();
        foreach (var (type, model) in models)
        {
            if (_entityModels.TryGetValue(type, out var existing))
            {
                existing.SetStreamTableType(model.GetExplicitStreamTableType());
                existing.UseManualCommit = model.UseManualCommit;
                existing.ErrorAction = model.ErrorAction;
                existing.DeserializationErrorPolicy = model.DeserializationErrorPolicy;
                existing.EnableCache = model.EnableCache;
                existing.BarTimeSelector = model.BarTimeSelector;
            }
            else
            {
                _entityModels[type] = model;
            }

            // Register property metadata with MappingRegistry
            _mappingRegistry.RegisterEntityModel(model);
        }
    }

    private EntityModel GetOrCreateEntityModel<T>() where T : class
    {
        return GetOrCreateEntityModel(typeof(T));
    }

    private EntityModel GetOrCreateEntityModel(Type entityType)
    {
        if (_entityModels.TryGetValue(entityType, out var existingModel))
        {
            return existingModel;
        }

        var entityModel = CreateEntityModelFromType(entityType);
        _entityModels[entityType] = entityModel;
        return entityModel;
    }

    private EntityModel CreateEntityModelFromType(Type entityType)
    {
        var allProperties = entityType.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        var keyProperties = System.Array.Empty<System.Reflection.PropertyInfo>();

        var model = new EntityModel
        {
            EntityType = entityType,
            TopicName = entityType.Name.ToLowerInvariant(),
            Partitions = 1,
            ReplicationFactor = 1,
            AllProperties = allProperties,
            KeyProperties = keyProperties
        };

        var validation = new ValidationResult { IsValid = true };

        if (keyProperties.Length == 0)
        {
            validation.Warnings.Add($"No key properties defined for {entityType.Name}");
        }

        model.ValidationResult = validation;

        return model;
    }


    /// <summary>
    /// OnModelCreating → execute automatic schema registration flow
    /// </summary>
    private void InitializeWithSchemaRegistration()
    {
        // 1. Build the model in OnModelCreating
        ConfigureModel();

        // [Naruse指示] Register schemas and materialize entities if new
        RegisterSchemasAndMaterializeAsync().GetAwaiter().GetResult();

        // 2. Verify Kafka connectivity
        ValidateKafkaConnectivity();

        EnsureKafkaReadyAsync().GetAwaiter().GetResult();
    }
    private async Task EnsureKafkaReadyAsync()
    {
        try
        {
            // Auto-create DLQ topic
            await _adminService.EnsureDlqTopicExistsAsync();

            // Additional connectivity check (performed by AdminService)
            _adminService.ValidateKafkaConnectivity();

            await _adminService.EnsureWindowFinalTopicsExistAsync(GetEntityModels());

            // Log output: DLQ preparation complete
            Logger.LogInformation("Kafka initialization completed; DLQ topic '{Topic}' ready with 5-second retention", GetDlqTopicName());
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

    /// <summary>
    /// Register schemas for all entities and send dummy record if newly created
    /// </summary>
    private async Task RegisterSchemasAndMaterializeAsync()
    {
        var client = _schemaRegistryClient.Value;

        foreach (var (type, model) in _entityModels)
        {
            if (type == typeof(Core.Models.DlqEnvelope))
                continue;

            if (model.QueryExpression != null)
            {
                await EnsureQueryEntityDdlAsync(type, model);
            }
            else
            {
                await EnsureSimpleEntityDdlAsync(type, model);
            }

            var subject = $"{model.GetTopicName()}-value";
            var subjects = await client.GetAllSubjectsAsync();

            if (!subjects.Contains(subject))
            {
                try
                {
                    var dummy = CreateDummyInstance(type);
                    var headers = new Dictionary<string, string> { ["is_dummy"] = "true" };
                    dynamic set = GetEventSet(type);
                    await set.AddAsync((dynamic)dummy, headers);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Materialization failed for {Entity}", type.Name);
                    throw;
                }
            }
        }
    }

    /// <summary>
    /// Create topics and ksqlDB objects for an entity defined without queries.
    /// </summary>
    private async Task EnsureSimpleEntityDdlAsync(Type type, EntityModel model)
    {
        var warnings = model.ValidationResult?.Warnings ?? new List<string>();
        if (warnings.Any(w => w.StartsWith("QuerySchema:")))
            return;

        var generator = new Kafka.Ksql.Linq.Query.Pipeline.DDLQueryGenerator();

        var topic = model.GetTopicName();
        var partitions = 1;
        short replicas = 1;
        if (_dslOptions.Topics.TryGetValue(topic, out var config) && config.Creation != null)
        {
            partitions = config.Creation.NumPartitions;
            replicas = config.Creation.ReplicationFactor;
        }

        model.Partitions = partitions;
        model.ReplicationFactor = replicas;

        await _adminService.CreateDbTopicAsync(topic, partitions, replicas);

        string ddl;
        var schemaProvider = new Query.Ddl.EntityModelDdlAdapter(model);
        ddl = model.StreamTableType == StreamTableType.Table
            ? generator.GenerateCreateTable(schemaProvider)
            : generator.GenerateCreateStream(schemaProvider);

        var result = await ExecuteStatementAsync(ddl);
        if (!result.IsSuccess)
        {
            Logger.LogWarning("DDL execution failed for {Entity}: {Message}", type.Name, result.Message);
        }
    }

    /// <summary>
    /// Generate and execute CREATE TABLE/STREAM AS statements for query entities.
    /// </summary>
    private async Task EnsureQueryEntityDdlAsync(Type type, EntityModel model)
    {
        var schema = Application.KsqlContextQueryExtensions.GetQuerySchema(this, type);
        if (schema == null || model.QueryExpression == null)
            return;

        if (!_entityModels.TryGetValue(schema.SourceType, out var sourceModel))
            return;

        var generator = new Query.Pipeline.DDLQueryGenerator();
        var objectName = model.GetTopicName();
        var baseObject = sourceModel.GetTopicName();

        string ddl = model.StreamTableType == StreamTableType.Table
            ? generator.GenerateCreateTableAs(objectName, baseObject, model.QueryExpression)
            : generator.GenerateCreateStreamAs(objectName, baseObject, model.QueryExpression);
        var result = await ExecuteStatementAsync(ddl);
        if (!result.IsSuccess)
        {
            Logger.LogWarning("DDL execution failed for {Entity}: {Message}", type.Name, result.Message);
        }
    }

    private string GetSubjectName(EntityModel model, KeyValueTypeMapping mapping)
    {
        return $"{mapping.ValueType.Namespace}.{mapping.ValueType.Name}";
    }

    private static string BuildSchemaString(Type entityType)
    {
        return Messaging.Internal.DynamicSchemaGenerator.GetSchemaJson(entityType);
    }

    private static object CreateDummyInstance(Type entityType)
    {
        var method = typeof(Application.DummyObjectFactory).GetMethod("CreateDummy")!
            .MakeGenericMethod(entityType);
        return method.Invoke(null, null)!;
    }


    /// <summary>
    /// SchemaRegistryClient作成
    /// </summary>
    private ConfluentSchemaRegistry.ISchemaRegistryClient CreateSchemaRegistryClient()
    {
        var options = _dslOptions.SchemaRegistry;
        var config = new ConfluentSchemaRegistry.SchemaRegistryConfig
        {
            Url = options.Url,
            MaxCachedSchemas = options.MaxCachedSchemas,
            RequestTimeoutMs = options.RequestTimeoutMs
        };

        return new ConfluentSchemaRegistry.CachedSchemaRegistryClient(config);
    }


    private Uri GetDefaultKsqlDbUrl()
    {
        if (!string.IsNullOrWhiteSpace(_dslOptions.KsqlDbUrl) &&
            Uri.TryCreate(_dslOptions.KsqlDbUrl, UriKind.Absolute, out var configured))
        {
            return configured;
        }

        var schemaUrl = _dslOptions.SchemaRegistry.Url;
        if (!string.IsNullOrWhiteSpace(schemaUrl) &&
            Uri.TryCreate(schemaUrl, UriKind.Absolute, out var schemaUri))
        {
            var port = schemaUri.IsDefaultPort || schemaUri.Port == 8081 ? 8088 : schemaUri.Port;
            return new Uri($"{schemaUri.Scheme}://{schemaUri.Host}:{port}");
        }

        throw new InvalidOperationException(
            "KsqlDbUrl or SchemaRegistry.Url is required to resolve the ksqlDB endpoint.");
    }
    private HttpClient CreateClient()
    {
        return new HttpClient { BaseAddress = GetDefaultKsqlDbUrl() };
    }

    public async Task<KsqlDbResponse> ExecuteStatementAsync(string statement)
    {
        var client = _ksqlDbClient.Value;
        var payload = new { ksql = statement, streamsProperties = new { } };
        var json = JsonSerializer.Serialize(payload);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/ksql", content);
        var body = await response.Content.ReadAsStringAsync();
        var success = response.IsSuccessStatusCode && !body.Contains("\"error_code\"");
        return new KsqlDbResponse(success, body);
    }

    public Task<KsqlDbResponse> ExecuteExplainAsync(string ksql)
    {
        return ExecuteStatementAsync($"EXPLAIN {ksql}");
    }



    /// <summary>
    /// Core層EventSet実装（上位層機能統合）
    /// </summary>
    protected virtual IEntitySet<T> CreateEntitySet<T>(EntityModel entityModel) where T : class
    {
        var baseSet = new EventSetWithServices<T>(this, entityModel);
        if (entityModel.GetExplicitStreamTableType() == StreamTableType.Table && entityModel.EnableCache)
        {
            return new ReadCachedEntitySet<T>(this, entityModel, null, baseSet);
        }
        return baseSet;
    }

    internal KafkaProducerManager GetProducerManager() => _producerManager;
    internal KafkaConsumerManager GetConsumerManager() => _consumerManager;
    internal DlqProducer GetDlqProducer() => _dlqProducer;
    internal ConfluentSchemaRegistry.ISchemaRegistryClient GetSchemaRegistryClient() => _schemaRegistryClient.Value;
    internal MappingRegistry GetMappingRegistry() => _mappingRegistry;

    private IEntitySet<Core.Models.DlqEnvelope> GetDlqStream()
    {
        var type = typeof(Core.Models.DlqEnvelope);
        if (_entitySets.TryGetValue(type, out var existing))
        {
            return (IEntitySet<Core.Models.DlqEnvelope>)existing;
        }

        var model = GetOrCreateEntityModel<Core.Models.DlqEnvelope>();
        var set = CreateEntitySet<Core.Models.DlqEnvelope>(model);
        _entitySets[type] = set;
        return set;
    }

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

    internal async Task<bool> IsEntityReadyAsync<T>(CancellationToken cancellationToken = default) where T : class
    {
        var models = GetEntityModels();
        if (!models.TryGetValue(typeof(T), out var model))
            return false;

        var statement = model.GetExplicitStreamTableType() == StreamTableType.Table
            ? "SHOW TABLES;"
            : "SHOW STREAMS;";

        var name = (model.TopicName ?? typeof(T).Name).ToUpperInvariant();
        var response = await ExecuteStatementAsync(statement);
        if (!response.IsSuccess)
            return false;

        try
        {
            using var doc = JsonDocument.Parse(response.Message);
            var listName = statement.Contains("TABLES") ? "tables" : "streams";
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (!item.TryGetProperty(listName, out var arr))
                    continue;

                foreach (var element in arr.EnumerateArray())
                {
                    if (element.TryGetProperty("name", out var n) &&
                        string.Equals(n.GetString(), name, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
        }
        catch
        {
            // ignore parse errors
        }

        return false;
    }

    public async Task WaitForEntityReadyAsync<T>(TimeSpan timeout, CancellationToken cancellationToken = default) where T : class
    {
        var start = DateTime.UtcNow;
        while (DateTime.UtcNow - start < timeout)
        {
            if (await IsEntityReadyAsync<T>(cancellationToken))
                return;

            await Task.Delay(100, cancellationToken);
        }

        throw new TimeoutException($"Entity {typeof(T).Name} not ready after {timeout}.");
    }

    public ConsumerBuilder<object, T> CreateConsumerBuilder<T>(KafkaSubscriptionOptions? options = null) where T : class
        => _consumerManager.CreateConsumerBuilder<T>(options);

    public ProducerBuilder<object, T> CreateProducerBuilder<T>(string? topicName = null) where T : class
        => _producerManager.CreateProducerBuilder<T>(topicName);

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            foreach (var entitySet in _entitySets.Values)
            {
                if (entitySet is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            _entitySets.Clear();
            _entityModels.Clear();
            _disposed = true;

            _producerManager?.Dispose();
            _consumerManager?.Dispose();
            _dlqProducer?.Dispose();
            _adminService?.Dispose();
            _cacheRegistry?.Dispose();

            if (_schemaRegistryClient.IsValueCreated)
            {
                _schemaRegistryClient.Value?.Dispose();
            }
            if (_ksqlDbClient.IsValueCreated)
            {
                _ksqlDbClient.Value.Dispose();
            }
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore();
        Dispose(false);
        GC.SuppressFinalize(this);
    }

    protected virtual async ValueTask DisposeAsyncCore()
    {
        foreach (var entitySet in _entitySets.Values)
        {
            if (entitySet is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
            else if (entitySet is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        _entitySets.Clear();

        _producerManager?.Dispose();
        _consumerManager?.Dispose();
        _dlqProducer?.Dispose();
        _adminService?.Dispose();
        _cacheRegistry?.Dispose();

        if (_schemaRegistryClient.IsValueCreated)
        {
            _schemaRegistryClient.Value?.Dispose();
        }
        if (_ksqlDbClient.IsValueCreated)
        {
            _ksqlDbClient.Value.Dispose();
        }

        await Task.CompletedTask;
    }

    public override string ToString()
    {
        return $"KafkaContextCore: {_entityModels.Count} entities, {_entitySets.Count} sets [schema auto-registration ready]";
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
    public async Task AddAsync(T entity, Dictionary<string, string>? headers = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var producerManager = _ksqlContext.GetProducerManager();

            await producerManager.SendAsync(entity, headers, cancellationToken);
        }
        catch (ConfluentSchemaRegistry.SchemaRegistryException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to send entity {typeof(T).Name} to Kafka", ex);
        }
    }

    public async Task RemoveAsync(T entity, CancellationToken cancellationToken = default)
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        try
        {
            var producerManager = _ksqlContext.GetProducerManager();
            await producerManager.DeleteAsync(entity, cancellationToken);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to delete entity {typeof(T).Name} from Kafka", ex);
        }
    }

    /// <summary>
    /// Consumer機能：Kafkaからエンティティリストを取得
    /// </summary>
    public async Task<List<T>> ToListAsync(CancellationToken cancellationToken = default)
    {
        if (_entityModel.EntityType == typeof(Core.Models.DlqEnvelope))
            throw new InvalidOperationException("DLQは無限列挙/履歴列であり、バッチ取得・件数指定取得は現状未対応です");

        if (_entityModel.GetExplicitStreamTableType() == StreamTableType.Stream)
            throw new InvalidOperationException(
                "ToListAsync() is not supported on a Stream source. Use ForEachAsync or subscribe for event consumption.");
        try
        {
            var cache = _ksqlContext.GetTableCache<T>();
            if (cache != null && _entityModel.GetExplicitStreamTableType() == StreamTableType.Table && _entityModel.EnableCache)
            {
                if (!cache.IsRunning)
                    throw new InvalidOperationException($"Cache for {typeof(T).Name} is not running");

                var list = new List<T>();
                foreach (var kv in cache.GetAll())
                {
                    if (kv.Value != null)
                        list.Add(kv.Value);
                }
                return list;
            }

            var consumerManager = _ksqlContext.GetConsumerManager();

            // Simplified implementation: call the actual Consumer
            // TODO: integrate with the actual Consumer implementation
            await Task.Delay(100, cancellationToken); // シミュレート

            return new List<T>();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to consume entities {typeof(T).Name} from Kafka", ex);
        }
    }
    private readonly ConcurrentDictionary<object, TopicPartitionOffset> _uncommitted
    = new ConcurrentDictionary<object, TopicPartitionOffset>();

    public async Task CommitAsync(T poco)
    {
        if (_uncommitted.TryGetValue(poco, out var commitOffset))
        {
            await _ksqlContext.GetConsumerManager().GetConsumerAsync<T>().Result.CommitAsync(commitOffset);
            var offsetsToRemove = _uncommitted
                  .Where(kv => kv.Value.Offset <= commitOffset.Offset)
                  .Select(kv => kv.Key)
                  .ToList();

            foreach (var key in offsetsToRemove)
            {
                _uncommitted.TryRemove(key, out _);
            }
        }
        else
        {
            throw new InvalidOperationException("Commit対象が見つかりません");
        }
    }
    /// <summary>
    /// Streaming機能：各エンティティに対してアクションを実行
    /// </summary>
    public async Task ForEachAsync(Func<T, Task> action, TimeSpan timeout = default, CancellationToken cancellationToken = default)
    {
        if (_entityModel.GetExplicitStreamTableType() == StreamTableType.Table)
            throw new InvalidOperationException("ForEachAsync() is not supported on a Table source. Use ToListAsync to obtain the full snapshot.");
        using var cts = (timeout != default && timeout != Timeout.InfiniteTimeSpan)
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : null;
        if (cts != null)
            cts.CancelAfter(timeout);

        var effectiveToken = cts?.Token ?? cancellationToken;
        try
        {
            var consumerManager = _ksqlContext.GetConsumerManager();
            await foreach (var item in consumerManager.ConsumeAsync<T>(cancellationToken))
            {
                if (item.Headers != null && item.Headers.TryGetLastBytes("is_dummy", out var headerValue))
                {
                    var isDummy = Encoding.UTF8.GetString(headerValue) == "true";
                    if (isDummy)
                    {
                        // ダミーメッセージはスキップ
                        continue;
                    }
                }
                if (_entityModel.UseManualCommit)
                {
                    _uncommitted.TryAdd(item.Value, item.GetTopicPartitionOffset());
                }

                await action(item.Value);
            }
        }
        catch (OperationCanceledException) when (cts != null && cts.IsCancellationRequested)
        {
            // タイムアウトで終了（正常系として握りつぶすか、ログに残すか、任意の対応）
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to stream entities {typeof(T).Name} from Kafka", ex);
        }
    }

    public async Task ForEachAsync(Func<T, KafkaMessageContext, Task> action, TimeSpan timeout = default, CancellationToken cancellationToken = default)
    {
        if (_entityModel.GetExplicitStreamTableType() == StreamTableType.Table)
            throw new InvalidOperationException("ForEachAsync() is not supported on a Table source. Use ToListAsync to obtain the full snapshot.");

        using var cts = (timeout != default && timeout != Timeout.InfiniteTimeSpan)
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : null;
        if (cts != null)
            cts.CancelAfter(timeout);

        var effectiveToken = cts?.Token ?? cancellationToken;

        try
        {
            var consumerManager = _ksqlContext.GetConsumerManager();
            
            // ここは「KafkaMessageContextも返す」IAsyncEnumerableをConsumerManagerで用意
            await foreach (var (item, context) in consumerManager.ConsumeWithContextAsync<T>(effectiveToken))
            {
                await action(item, context);
            }
        }
        catch (OperationCanceledException) when (cts != null && cts.IsCancellationRequested)
        {
            // タイムアウト終了（握りつぶし or ログ or 何もしない）
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
        var consumerManager = _ksqlContext.GetConsumerManager();
        await foreach (var item in consumerManager.ConsumeAsync<T>(cancellationToken))
        {
            yield return item.Value;
        }
    }

    protected virtual IManualCommitMessage<T> CreateManualCommitMessage(T item)
        => new ManualCommitMessage<T>(item, () => Task.CompletedTask, () => Task.CompletedTask);

    //public async IAsyncEnumerable<object> ForEachAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    //{
    //    return ForEachAsync(cancellationToken);
    //}

    // Metadata取得
    public string GetTopicName() => (_entityModel.TopicName ?? typeof(T).Name).ToLowerInvariant();
    public EntityModel GetEntityModel() => _entityModel;
    public IKsqlContext GetContext() => _ksqlContext;

    public override string ToString()
    {
        return $"EventSetWithServices<{typeof(T).Name}> - Topic: {GetTopicName()}";
    }

    public Task ForEachAsync(Func<T, KafkaMessage<T, object>, Task> action, TimeSpan timeout = default, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}

