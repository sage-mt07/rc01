using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Core.Window;
using System;

namespace Kafka.Ksql.Linq.Importer;

public static class WindowExtensions
{
    /// <summary>
    /// 指定したウィンドウサイズでのウィンドウEntitySetを取得
    /// </summary>
    /// <typeparam name="T">エンティティ型</typeparam>
    /// <param name="entitySet">ベースEntitySet</param>
    /// <param name="windowMinutes">ウィンドウサイズ（分）</param>
    /// <returns>ウィンドウ化されたEntitySet</returns>
    public static IWindowedEntitySet<T> Window<T>(this IEntitySet<T> entitySet, int windowMinutes)
        where T : class
    {
        if (windowMinutes <= 0)
            throw new ArgumentException("Window size must be positive", nameof(windowMinutes));

        return new WindowedEntitySet<T>(entitySet, windowMinutes);
    }

    /// <summary>
    /// 複数のウィンドウサイズを一括設定
    /// </summary>
    /// <typeparam name="T">エンティティ型</typeparam>
    /// <param name="entitySet">ベースEntitySet</param>
    /// <param name="windowSizes">ウィンドウサイズ配列（分）</param>
    /// <returns>ウィンドウコレクション</returns>
    public static IWindowCollection<T> Windows<T>(this IEntitySet<T> entitySet, params int[] windowSizes)
        where T : class
    {
        if (windowSizes == null || windowSizes.Length == 0)
            throw new ArgumentException("At least one window size is required", nameof(windowSizes));

        return new WindowCollection<T>(entitySet, windowSizes);
    }
}
