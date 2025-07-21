using Kafka.Ksql.Linq.Core.Abstractions;
using System;
using System.Linq;
using System.Linq.Expressions;

namespace Kafka.Ksql.Linq.Core.Modeling;


/// <summary>
/// ModelBuilderにクエリ定義用ヘルパーメソッドを追加
/// </summary>
public static class ModelBuilderQueryExtensions
{
    /// <summary>
    /// クエリベースのエンティティ定義を開始
    /// </summary>
    public static EntityModelBuilder<T> DefineQuery<T>(this IModelBuilder modelBuilder)
        where T : class
    {
        var entityBuilder = modelBuilder.Entity<T>();
        return (EntityModelBuilder<T>)entityBuilder;
    }

    /// <summary>
    /// ソース型からターゲット型へのクエリ定義
    /// </summary>
    public static EntityModelBuilder<TTarget> DefineQuery<TSource, TTarget>(
        this IModelBuilder modelBuilder,
        Expression<Func<IQueryable<TSource>, IQueryable<TTarget>>> queryExpression)
        where TSource : class
        where TTarget : class
    {
        var entityBuilder = (EntityModelBuilder<TTarget>)modelBuilder.Entity<TTarget>();
        return entityBuilder.HasQuery(queryExpression);
    }
}
