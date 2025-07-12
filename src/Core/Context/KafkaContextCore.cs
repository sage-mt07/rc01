using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Core.Modeling;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace Kafka.Ksql.Linq.Core.Context;

/// <summary>
/// KafkaContext基底実装
/// 責務: モデル構築、エンティティセット管理
/// 制約: 完全ログフリー、副作用なし（Phase3 ログフリー版）
/// ログ処理: Infrastructure層で実装
/// </summary>
public abstract class KafkaContextCore : IKsqlContext
{
    private readonly Dictionary<Type, EntityModel> _entityModels = new();
    private readonly Dictionary<Type, object> _entitySets = new();
    protected readonly KafkaContextOptions Options;
    private bool _disposed = false;

    protected KafkaContextCore()
    {
        Options = new KafkaContextOptions();
        InitializeEntityModels();
    }

    protected KafkaContextCore(KafkaContextOptions options)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
        InitializeEntityModels();
    }
    protected virtual void OnModelCreating(IModelBuilder modelBuilder) { }
    // ✅ IKsqlContext実装: エンティティセット取得（純粋関数）
    public IEntitySet<T> Set<T>() where T : class
    {
        var entityType = typeof(T);

        if (_entitySets.TryGetValue(entityType, out var existingSet))
        {
            return (IEntitySet<T>)existingSet;
        }

        var entityModel = GetOrCreateEntityModel<T>();
        var entitySet = CreateEntitySet<T>(entityModel);
        _entitySets[entityType] = entitySet;

        return entitySet;
    }

    // ✅ IKsqlContext実装: 非ジェネリック エンティティセット取得
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

    // ✅ IKsqlContext実装: エンティティモデル取得（純粋関数）
    public Dictionary<Type, EntityModel> GetEntityModels()
    {
        return new Dictionary<Type, EntityModel>(_entityModels);
    }

    // ✅ 派生クラスでの実装必須（純粋関数）
    protected abstract IEntitySet<T> CreateEntitySet<T>(EntityModel entityModel) where T : class;

    // ✅ 内部ヘルパー：リフレクション版エンティティセット作成
    protected virtual object CreateEntitySet(Type entityType, EntityModel entityModel)
    {
        var method = GetType().GetMethod(nameof(CreateEntitySet), 1, new[] { typeof(EntityModel) });
        var genericMethod = method!.MakeGenericMethod(entityType);
        return genericMethod.Invoke(this, new object[] { entityModel })!;
    }
    protected void ConfigureModel()
    {
        var modelBuilder = new ModelBuilder(Options.ValidationMode);
        using (Kafka.Ksql.Linq.Core.Modeling.ModelCreatingScope.Enter())
        {
            OnModelCreating(modelBuilder);
        }
        ApplyModelBuilderSettings(modelBuilder);
    }

    // ✅ 内部処理：モデル初期化（副作用なし）
    private void InitializeEntityModels()
    {// OnModelCreatingは呼び出さない（基本の属性ベース初期化のみ）
     // Fluent APIが必要な場合のみ派生クラスで明示的に呼び出し
     //var modelBuilder = new ModelBuilder(Options.ValidationMode);
     //OnModelCreating((IModelBuilder)modelBuilder);
     //ApplyModelBuilderSettings(modelBuilder);
    }

    private void ApplyModelBuilderSettings(ModelBuilder modelBuilder)
    {
        var models = modelBuilder.GetAllEntityModels();
        foreach (var (type, model) in models)
        {
            if (_entityModels.TryGetValue(type, out var existing))
            {
                // Stream/Table 型のみ上書き
                existing.SetStreamTableType(model.GetExplicitStreamTableType());
            }
            else
            {
                _entityModels[type] = model;
            }
        }
    }

    // ✅ 内部処理：エンティティモデル取得・作成（純粋関数）
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

    // ✅ 内部処理：型からエンティティモデル作成（純粋関数）
    private EntityModel CreateEntityModelFromType(Type entityType)
    {
        var allProperties = entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var keyProperties = Array.Empty<PropertyInfo>();

        var model = new EntityModel
        {
            EntityType = entityType,
            TopicName = entityType.Name.ToLowerInvariant(),
            AllProperties = allProperties,
            KeyProperties = keyProperties
        };

        // ✅ 基本検証（副作用なし）
        var validation = new ValidationResult { IsValid = true };

        if (keyProperties.Length == 0)
        {
            validation.Warnings.Add($"No key properties defined for {entityType.Name}");
        }

        model.ValidationResult = validation;

        return model;
    }

    // ✅ リソース解放（IDisposable実装）
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

            // ❌ ログ出力なし: Options.EnableDebugLogging のチェックも削除
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    // ✅ 非同期リソース解放（IAsyncDisposable実装）
    public virtual async ValueTask DisposeAsync()
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
        await Task.CompletedTask;
    }

    // ✅ デバッグ用（副作用なし）
    public override string ToString()
    {
        return $"KafkaContextCore: {_entityModels.Count} entities, {_entitySets.Count} sets";
    }
}
