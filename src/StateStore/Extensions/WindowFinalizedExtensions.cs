namespace Kafka.Ksql.Linq.StateStore.Extensions;

internal static class WindowFinalizedExtensions
{
    internal static Kafka.Ksql.Linq.StateStore.IWindowedEntitySet<T> UseFinalized<T>(this Kafka.Ksql.Linq.StateStore.IWindowedEntitySet<T> windowSet) where T : class
    {
        var context = windowSet.GetContext();
        var manager = context.GetStateStoreManager();
        if (manager == null)
            return windowSet;

        var store = manager.GetOrCreateStore<string, T>(windowSet.GetEntityModel().EntityType, windowSet.WindowMinutes);
        return new ReadCachedWindowSet<T>(windowSet, store);
    }
}
