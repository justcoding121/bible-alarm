#nullable enable

using System;
using System.Collections;

namespace Bible.Alarm.Shared.Helpers;

/// <summary>
/// Enumerable inspection rules shared by CollectionView readiness checks (no MAUI types).
/// </summary>
public static class ItemsSourcePresence
{
    public static bool HasAnyItems(object? itemsSource)
    {
        if (itemsSource == null)
        {
            return false;
        }

        try
        {
            if (itemsSource is ICollection collection)
            {
                return collection.Count > 0;
            }

            if (itemsSource is IEnumerable enumerable)
            {
                var enumerator = enumerable.GetEnumerator();
                try
                {
                    return enumerator.MoveNext();
                }
                finally
                {
                    if (enumerator is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
            }
        }
        catch (Exception)
        {
            return false;
        }

        return false;
    }

    public static bool ContainsItem(object? itemsSource, object? item)
    {
        if (itemsSource == null || item == null)
        {
            return false;
        }

        try
        {
            if (itemsSource is IEnumerable enumerable)
            {
                foreach (var sourceItem in enumerable)
                {
                    if (ReferenceEquals(sourceItem, item))
                    {
                        return true;
                    }

                    if (sourceItem?.Equals(item) is true)
                    {
                        return true;
                    }
                }
            }
        }
        catch (Exception)
        {
            return false;
        }

        return false;
    }
}
