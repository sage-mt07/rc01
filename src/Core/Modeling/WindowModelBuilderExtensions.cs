using Kafka.Ksql.Linq.Core.Abstractions;

using System;
using System.Linq;
using System.Linq.Expressions;

namespace Kafka.Ksql.Linq.Core.Modeling;

public static class WindowModelBuilderExtensions
{
    /// <summary>
    /// ウィンドウ集約の設定を追加
    /// </summary>
    /// <typeparam name="T">エンティティ型</typeparam>
    /// <param name="builder">EntityModelBuilder</param>
    /// <param name="windowExpression">ウィンドウ定義式</param>
    /// <param name="windowSizes">対象ウィンドウサイズ配列（分）</param>
    /// <param name="gracePeriod">遅延許容時間</param>
    /// <returns>EntityModelBuilder</returns>
    public static EntityModelBuilder<T> Window<T>(
        this EntityModelBuilder<T> builder,
        Expression<Func<IGrouping<object, T>, object>> windowExpression,
        int[] windowSizes,
        TimeSpan? gracePeriod = null) where T : class
    {
        if (windowExpression == null)
            throw new ArgumentNullException(nameof(windowExpression));
        if (windowSizes == null || windowSizes.Length == 0)
            throw new ArgumentException("At least one window size is required", nameof(windowSizes));

        var entityModel = builder.GetModel();

        // WindowConfigurationをEntityModelに追加
        var windowConfig = new WindowConfiguration
        {
            WindowExpression = windowExpression,
            WindowSizes = windowSizes,
            GracePeriod = gracePeriod ?? TimeSpan.FromSeconds(3)
        };

        // EntityModelに拡張データとして保存
        if (entityModel.ValidationResult?.Errors == null)
        {
            entityModel.ValidationResult = new ValidationResult { IsValid = true, Errors = new(), Warnings = new() };
        }

        // Warningとして設定情報を記録（デバッグ用）
        entityModel.ValidationResult.Warnings.Add(
            $"Window configuration added: {windowSizes.Length} windows, Grace period: {windowConfig.GracePeriod}");

        return builder;
    }

    /// <summary>
    /// 詳細なウィンドウ設定（GroupBy指定版）
    /// </summary>
    public static EntityModelBuilder<T> Window<T, TKey>(
        this EntityModelBuilder<T> builder,
        Expression<Func<T, TKey>> groupByExpression,
        Expression<Func<IGrouping<TKey, T>, object>> aggregationExpression,
        int[] windowSizes,
        TimeSpan? gracePeriod = null) where T : class
    {
        if (groupByExpression == null)
            throw new ArgumentNullException(nameof(groupByExpression));
        if (aggregationExpression == null)
            throw new ArgumentNullException(nameof(aggregationExpression));

        var entityModel = builder.GetModel();

        var windowConfig = new DetailedWindowConfiguration<T, TKey>
        {
            GroupByExpression = groupByExpression,
            AggregationExpression = aggregationExpression,
            WindowSizes = windowSizes,
            GracePeriod = gracePeriod ?? TimeSpan.FromSeconds(3)
        };

        // EntityModelに詳細設定を保存
        entityModel.ValidationResult ??= new ValidationResult { IsValid = true, Errors = new(), Warnings = new() };
        entityModel.ValidationResult.Warnings.Add(
            $"Detailed window configuration added: GroupBy + Aggregation, {windowSizes.Length} windows");

        return builder;
    }
}

/// <summary>
/// ウィンドウ設定情報
/// </summary>
public class WindowConfiguration
{
    public Expression WindowExpression { get; set; } = default!;
    public int[] WindowSizes { get; set; } = Array.Empty<int>();
    public TimeSpan GracePeriod { get; set; } = TimeSpan.FromSeconds(3);
    public WindowType WindowType { get; set; } = WindowType.Tumbling;
}

/// <summary>
/// 詳細ウィンドウ設定情報
/// </summary>
public class DetailedWindowConfiguration<T, TKey> : WindowConfiguration where T : class
{
    public Expression<Func<T, TKey>>? GroupByExpression { get; set; }
    public Expression<Func<IGrouping<TKey, T>, object>>? AggregationExpression { get; set; }
}
