using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace Kafka.Ksql.Linq.Core.Abstractions;

public interface IWindowedEntitySet<T> : IEntitySet<T> where T : class
{
    /// <summary>
    /// ウィンドウサイズ（分）
    /// </summary>
    int WindowMinutes { get; }

    /// <summary>
    /// ウィンドウ集約の設定
    /// </summary>
    /// <param name="aggregationExpression">集約式</param>
    /// <param name="gracePeriod">遅延許容時間（デフォルト: 3秒）</param>
    /// <returns>集約されたEntitySet</returns>
    IEntitySet<TResult> Aggregate<TResult>(
        Expression<Func<IGrouping<object, T>, TResult>> aggregationExpression,
        TimeSpan? gracePeriod = null) where TResult : class;

    /// <summary>
    /// ウィンドウ集約の詳細設定
    /// </summary>
    /// <param name="groupByExpression">グループ化式</param>
    /// <param name="aggregationExpression">集約式</param>
    /// <param name="gracePeriod">遅延許容時間</param>
    /// <returns>集約されたEntitySet</returns>
    IEntitySet<TResult> GroupByAggregate<TKey, TResult>(
        Expression<Func<T, TKey>> groupByExpression,
        Expression<Func<IGrouping<TKey, T>, TResult>> aggregationExpression,
        TimeSpan? gracePeriod = null) where TResult : class;

    /// <summary>
    /// 生成されるKSQLテーブル名を取得
    /// </summary>
    string GetWindowTableName();
}

/// <summary>
/// 複数ウィンドウのコレクション
/// </summary>
public interface IWindowCollection<T> where T : class
{
    /// <summary>
    /// 指定したウィンドウサイズのEntitySetを取得
    /// </summary>
    IWindowedEntitySet<T> this[int windowMinutes] { get; }

    /// <summary>
    /// 設定されたウィンドウサイズ一覧
    /// </summary>
    int[] WindowSizes { get; }

    /// <summary>
    /// 全ウィンドウの集約結果を取得
    /// </summary>
    Task<Dictionary<int, List<T>>> GetAllWindowsAsync(CancellationToken cancellationToken = default);
}

// src/Core/Abstractions/WindowAggregationConfig.cs


/// <summary>
/// ウィンドウ集約の設定情報
/// </summary>
public record WindowAggregationConfig
{
    /// <summary>
    /// ウィンドウサイズ
    /// </summary>
    public TimeSpan WindowSize { get; set; }

    /// <summary>
    /// グレースピリオド（遅延許容時間）
    /// </summary>
    public TimeSpan GracePeriod { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>
    /// ウィンドウタイプ
    /// </summary>
    public WindowType WindowType { get; set; } = WindowType.Tumbling;

    /// <summary>
    /// 出力モード
    /// </summary>
    public WindowOutputMode OutputMode { get; set; } = WindowOutputMode.Changes;

    /// <summary>
    /// Heartbeatトピックの使用
    /// </summary>
    public bool UseHeartbeat { get; set; } = true;
}

/// <summary>
/// ウィンドウタイプ
/// </summary>
public enum WindowType
{
    Tumbling,
    Hopping,
    Session
}

/// <summary>
/// ウィンドウ出力モード
/// </summary>
public enum WindowOutputMode
{
    Changes,
    Final
}
