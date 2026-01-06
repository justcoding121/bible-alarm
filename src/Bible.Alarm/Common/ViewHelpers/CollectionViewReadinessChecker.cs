#nullable enable
using System.Collections;
using System.Linq;
using Serilog;
using Microsoft.Maui.Controls;
using MauiCollectionView = Microsoft.Maui.Controls.CollectionView;
#if WINDOWS
using Microsoft.UI.Xaml;
#endif

namespace Bible.Alarm.Common.ViewHelpers;

/// <summary>
/// Helper class for checking if a CollectionView is ready for scrolling operations.
/// </summary>
internal static class CollectionViewReadinessChecker
{
    /// <summary>
    /// Waits for the CollectionView to be ready for scrolling.
    /// </summary>
    public static async Task<bool> WaitForCollectionViewReadyAsync(MauiCollectionView collectionView, object item, CancellationToken cancellationToken = default)
    {
        const int MaxAttempts = 7;
        const int DelayMs = 100;

        // Ensure the item exists in the source before proceeding
        // If ObservableCollection was just updated, the native platform might not have realized the last item exists yet
        var items = collectionView.ItemsSource?.Cast<object>().ToList();
        if (items == null || !ItemExistsInSource(collectionView.ItemsSource!, item))
        {
            // Give the renderer one frame to catch up with the data change
            await Task.Yield();
        }

        for (var i = 0; i < MaxAttempts; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(DelayMs, cancellationToken);

            // Wait for the handler to be attached (Windows specific)
#if WINDOWS
            if (collectionView.Handler == null)
            {
                continue;
            }
#endif

            if (await IsCollectionViewReady(collectionView, item, cancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    private static async Task<bool> IsCollectionViewReady(MauiCollectionView collectionView, object item, CancellationToken cancellationToken)
    {
        try
        {
            if (!IsCollectionViewValid(collectionView, item))
            {
                return false;
            }

            return await CheckPlatformSpecificReady(collectionView, cancellationToken);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsCollectionViewValid(MauiCollectionView collectionView, object item)
    {
        if (collectionView.ItemsSource == null || !HasItems(collectionView.ItemsSource))
        {
            return false;
        }

        if (collectionView.Handler == null)
        {
            return false;
        }

        return ItemExistsInSource(collectionView.ItemsSource, item);
    }

    private static async Task<bool> CheckPlatformSpecificReady(MauiCollectionView collectionView, CancellationToken cancellationToken)
    {
#if WINDOWS
        if (DeviceInfo.Platform == DevicePlatform.WinUI)
        {
            return await WindowsNativeScrollHelper.CheckWindowsReady(collectionView, cancellationToken);
        }
#endif
        return await CheckOtherPlatformsReady(collectionView, cancellationToken);
    }

    private static async Task<bool> CheckOtherPlatformsReady(MauiCollectionView collectionView, CancellationToken cancellationToken)
    {
        if (collectionView.Handler?.PlatformView != null)
        {
            await Task.Delay(200, cancellationToken);
            return true;
        }
        return false;
    }

    private static bool HasItems(object itemsSource)
    {
        if (itemsSource == null)
        {
            return false;
        }

        try
        {
            // Handle ICollection for count
            if (itemsSource is ICollection collection)
            {
                return collection.Count > 0;
            }

            // Handle IEnumerable - check if it has any items
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
        catch
        {
            return false;
        }

        return false;
    }

    private static bool ItemExistsInSource(object itemsSource, object item)
    {
        if (itemsSource == null || item == null)
        {
            return false;
        }

        try
        {
            // Handle IEnumerable collections
            if (itemsSource is IEnumerable enumerable)
            {
                foreach (var sourceItem in enumerable)
                {
                    // Use reference equality first (fastest)
                    if (ReferenceEquals(sourceItem, item))
                    {
                        return true;
                    }

                    // Use Equals for value comparison
                    if (sourceItem?.Equals(item) == true)
                    {
                        return true;
                    }
                }
            }
        }
        catch
        {
            return false;
        }

        return false;
    }
}
