using System;
using System.Linq.Expressions;

namespace Kafka.Ksql.Linq.Core.Abstractions;
public interface IThreeWayJoinResult<TOuter, TInner, TThird>
    where TOuter : class
    where TInner : class
    where TThird : class
{
    /// <summary>
    /// 3テーブル結合の結果射影
    /// </summary>
    IEntitySet<TResult> Select<TResult>(
        Expression<Func<TOuter, TInner, TThird, TResult>> resultSelector) where TResult : class;
}
