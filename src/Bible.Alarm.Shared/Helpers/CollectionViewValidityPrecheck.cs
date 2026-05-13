#nullable enable

namespace Bible.Alarm.Shared.Helpers;

/// <summary>
/// Non-MAUI preconditions for treating a CollectionView as ready for scroll operations on the logical items source.
/// </summary>
public static class CollectionViewValidityPrecheck
{
    public static bool HasRenderableItemsSourceWithHandler(object? itemsSource, bool handlerAttached, object item)
    {
        if (itemsSource == null || !ItemsSourcePresence.HasAnyItems(itemsSource))
        {
            return false;
        }

        if (!handlerAttached)
        {
            return false;
        }

        return ItemsSourcePresence.ContainsItem(itemsSource, item);
    }
}
