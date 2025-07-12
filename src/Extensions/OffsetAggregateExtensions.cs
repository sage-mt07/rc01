using System;
using System.Linq;
using System.Linq.Expressions;

namespace Kafka.Ksql.Linq
{
    /// <summary>
    /// KSQLのオフセット集約関数用拡張メソッド
    /// 実行時には使用されず、LINQ式解析専用
    /// </summary>
    public static class OffsetAggregateExtensions
    {
        public static TResult LatestByOffset<TSource, TKey, TResult>(this IGrouping<TKey, TSource> source, Expression<Func<TSource, TResult>> selector)
        {
            throw new NotSupportedException("LatestByOffset is for expression translation only.");
        }

        public static TResult EarliestByOffset<TSource, TKey, TResult>(this IGrouping<TKey, TSource> source, Expression<Func<TSource, TResult>> selector)
        {
            throw new NotSupportedException("EarliestByOffset is for expression translation only.");
        }
    }
}
