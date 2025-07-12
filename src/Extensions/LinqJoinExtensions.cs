namespace Kafka.Ksql.Linq.Extensions;

//public static class LinqJoinExtensions
//{
//    /// <summary>
//    /// EntitySetをJOIN可能にする
//    /// </summary>
//    public static IJoinableEntitySet<T> AsJoinable<T>(this IEntitySet<T> entitySet) where T : class
//    {
//        if (entitySet is IJoinableEntitySet<T> joinable)
//        {
//            return joinable;
//        }

//        return new JoinableEntitySet<T>(entitySet);
//    }

//    /// <summary>
//    /// クエリ構文サポート: from o in Orders join c in Customers
//    /// </summary>
//    public static IJoinResult<TOuter, TInner> Join<TOuter, TInner, TKey>(
//        this IEntitySet<TOuter> outer,
//        IEntitySet<TInner> inner,
//        Expression<Func<TOuter, TKey>> outerKeySelector,
//        Expression<Func<TInner, TKey>> innerKeySelector) where TOuter : class where TInner : class
//    {
//        return outer.AsJoinable().Join(inner, outerKeySelector, innerKeySelector);
//    }

//    /// <summary>
//    /// メソッド構文サポート: orders.Join(customers, ...)
//    /// </summary>
//    public static IJoinResult<TOuter, TInner> Join<TOuter, TInner, TKey>(
//        this IJoinableEntitySet<TOuter> outer,
//        IEntitySet<TInner> inner,
//        Expression<Func<TOuter, TKey>> outerKeySelector,
//        Expression<Func<TInner, TKey>> innerKeySelector) where TOuter : class where TInner : class
//    {
//        return outer.Join(inner, outerKeySelector, innerKeySelector);
//    }
//}
