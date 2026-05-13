#nullable enable

namespace Bible.Alarm.Shared.Helpers;

/// <summary>
/// Decides whether to yield once before polling CollectionView readiness so platform renderers can catch up with a just-updated items source.
/// </summary>
public static class CollectionViewItemsSourceWarmupGate
{
    public static bool ShouldYieldOnceBeforePolling(object? itemsSource, object item)
    {
        if (itemsSource == null || !ItemsSourcePresence.ContainsItem(itemsSource, item))
        {
            return true;
        }

        return false;
    }
}
