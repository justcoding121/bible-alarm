#if WINDOWS
using Microsoft.UI.Xaml;
#endif
using System.Collections;
using System.Linq;
using System.Runtime.InteropServices;
using Polly;
using Serilog;
using MauiCollectionView = Microsoft.Maui.Controls.CollectionView;

namespace Bible.Alarm.Common.ViewHelpers;

public static class CollectionViewHelper
{
    /// <summary>
    /// Waits for the CollectionView to be ready and then scrolls to the specified item.
    /// On Windows, this waits for the visual tree to be fully loaded before scrolling.
    /// Automatically uses ScrollToPosition.End for items in the last 20% of the list to ensure they are fully visible.
    /// </summary>
    public static async Task ScrollToWhenReadyAsync(MauiCollectionView collectionView, object item, ScrollToPosition position = ScrollToPosition.Center, bool animated = false, CancellationToken cancellationToken = default)
    {
        if (collectionView == null || item == null)
        {
            return;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var isReady = await WaitForCollectionViewReadyAsync(collectionView, item, cancellationToken);
            if (!isReady)
            {
                Log.Logger.Debug("CollectionView not ready for scrolling or item not found in ItemsSource");
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();

            var canScroll = await CanSafelyScrollAsync(collectionView, cancellationToken);
            if (!canScroll)
            {
                return;
            }

            // Determine the best scroll position based on item location in the list
            var finalPosition = DetermineOptimalScrollPosition(collectionView, item, position);

            await PerformScrollAsync(collectionView, item, finalPosition, animated, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Cancellation requested - this is expected, don't log
        }
        catch (Exception ex)
        {
            // Ignore errors - scrolling is not critical
            Log.Logger.Debug(ex, "Exception in ScrollToWhenReadyAsync - scrolling is not critical: {Message}", ex.Message);
        }
    }

    /// <summary>
    /// Determines the optimal scroll position based on where the item is located in the list.
    /// For items in the last 20% of the list, uses ScrollToPosition.End to ensure full visibility.
    /// </summary>
    private static ScrollToPosition DetermineOptimalScrollPosition(
        MauiCollectionView collectionView,
        object item,
        ScrollToPosition requestedPosition)
    {
        // If position is explicitly set to something other than Center, use it
        if (requestedPosition != ScrollToPosition.Center)
        {
            return requestedPosition;
        }

        try
        {
            // Get the ItemsSource to determine item position
            var itemsSource = collectionView.ItemsSource;
            if (itemsSource == null)
            {
                return requestedPosition;
            }

            // Convert to enumerable to find index
            var itemsList = itemsSource as IList ?? itemsSource.Cast<object>().ToList();
            if (itemsList == null || itemsList.Count == 0)
            {
                return requestedPosition;
            }

            // Find the index of the item
            int itemIndex = -1;
            for (int i = 0; i < itemsList.Count; i++)
            {
                if (ReferenceEquals(itemsList[i], item))
                {
                    itemIndex = i;
                    break;
                }
            }

            if (itemIndex == -1)
            {
                // Item not found, use requested position
                return requestedPosition;
            }

            // If item is in the last 20% of the list, use End to ensure it's fully visible
            // Also use End if it's within the last 10 items (for smaller lists)
            var totalItems = itemsList.Count;
            var isInLast20Percent = itemIndex >= totalItems * 0.8;
            var isInLast10Items = itemIndex >= totalItems - 10;

            if (isInLast20Percent || isInLast10Items)
            {
                Log.Logger.Debug("Item at index {ItemIndex} of {TotalItems} is near the end, using ScrollToPosition.End", itemIndex, totalItems);
                return ScrollToPosition.End;
            }

            return requestedPosition;
        }
        catch (Exception ex)
        {
            // If we can't determine position, fall back to requested position
            Log.Logger.Debug(ex, "Error determining optimal scroll position, using requested position: {Position}", requestedPosition);
            return requestedPosition;
        }
    }

    private static async Task<bool> CanSafelyScrollAsync(MauiCollectionView collectionView, CancellationToken cancellationToken)
    {
#if WINDOWS
        if (DeviceInfo.Platform == DevicePlatform.WinUI)
        {
            return await CanSafelyScrollWindows(collectionView, cancellationToken);
        }
#endif
        return true;
    }

    private static async Task PerformScrollAsync(
        MauiCollectionView collectionView,
        object item,
        ScrollToPosition position,
        bool animated,
        CancellationToken cancellationToken)
    {
        await Task.Run(async () =>
        {
            try
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (!ValidateCollectionViewBeforeScroll(collectionView, cancellationToken))
                    {
                        return;
                    }

                    collectionView.ScrollTo(item, position: position, animate: animated);
                    Log.Logger.Debug("Successfully scrolled to item in CollectionView");
                });

                // Small delay after scrolling to let the layout settle and prevent scrollbar jitter
                // This allows the CollectionView to complete any layout adjustments before the scrollbar fades
                await Task.Delay(50, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Cancellation requested - this is expected, don't log
            }
            catch (Exception ex)
            {
                HandleScrollException(ex);
            }
        }, cancellationToken);
    }

    private static bool ValidateCollectionViewBeforeScroll(MauiCollectionView collectionView, CancellationToken cancellationToken)
    {
        if (collectionView == null)
        {
            Log.Logger.Debug("CollectionView is null, skipping scroll");
            return false;
        }

        if (collectionView.Parent == null)
        {
            Log.Logger.Debug("CollectionView is not attached to parent, skipping scroll");
            return false;
        }

        if (collectionView.Handler == null || collectionView.Handler.PlatformView == null)
        {
            Log.Logger.Debug("CollectionView handler is null or disposed, skipping scroll");
            return false;
        }

        cancellationToken.ThrowIfCancellationRequested();
        return true;
    }

    private static void HandleScrollException(Exception ex)
    {
        switch (ex)
        {
            case OperationCanceledException:
                // Cancellation requested - this is expected, don't log
                break;
            case NullReferenceException:
                // Handler was disposed or ItemCount property is null - ignore the error
                Log.Logger.Debug(ex, "NullReferenceException while scrolling CollectionView - handler may be disposed");
                break;
            case COMException:
                // Visual tree/ScrollViewer not ready yet, ignore the error
                Log.Logger.Debug(ex, "COMException while scrolling CollectionView - visual tree not ready yet");
                break;
            default:
                // Other errors, log but don't throw
                Log.Logger.Debug(ex, "Exception while scrolling CollectionView: {Message}", ex.Message);
                break;
        }
    }

    private static async Task<bool> WaitForCollectionViewReadyAsync(MauiCollectionView collectionView, object item, CancellationToken cancellationToken = default)
    {
        const int MaxAttempts = 50;
        const int DelayMs = 100;

        for (var i = 0; i < MaxAttempts; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(DelayMs, cancellationToken);

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
        catch (Exception ex)
        {
            Log.Logger.Debug(ex, "Exception while waiting for CollectionView ready - visual tree not ready yet");
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
            return await CheckWindowsReady(collectionView, cancellationToken);
        }
#endif
        return await CheckOtherPlatformsReady(collectionView, cancellationToken);
    }

#if WINDOWS
    private static async Task<bool> CheckWindowsReady(MauiCollectionView collectionView, CancellationToken cancellationToken)
    {
        if (collectionView.Handler.PlatformView is FrameworkElement frameworkElement && frameworkElement.IsLoaded)
        {
            await Task.Delay(300, cancellationToken);
            return true;
        }
        return false;
    }
#endif

    private static async Task<bool> CheckOtherPlatformsReady(MauiCollectionView collectionView, CancellationToken cancellationToken)
    {
        if (collectionView.Handler.PlatformView != null)
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
        catch (Exception ex)
        {
            Log.Logger.Debug(ex, "Exception while checking if ItemsSource has items");
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
        catch (Exception ex)
        {
            Log.Logger.Debug(ex, "Exception while checking if item exists in ItemsSource");
            return false;
        }

        return false;
    }


#if WINDOWS
    private static async Task<bool> CanSafelyScrollWindows(MauiCollectionView collectionView, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (collectionView.Handler?.PlatformView is FrameworkElement frameworkElement)
            {
                if (!frameworkElement.IsLoaded)
                {
                    return false;
                }

                // Wait longer to ensure ScrollViewer is fully initialized
                // The ScrollViewer needs time to be created and added to the visual tree
                await Task.Delay(400, cancellationToken);
                return true;
            }
        }
        catch (OperationCanceledException)
        {
            // Cancellation requested - can't safely scroll
            return false;
        }
        catch (Exception ex)
        {
            // Can't safely scroll yet
            Log.Logger.Debug(ex, "Exception in CanSafelyScrollWindows - can't safely scroll yet");
            return false;
        }

        return false;
    }
#endif

    /// <summary>
    /// Waits for a ViewModel's IsBusy property to become false using Polly retry policy.
    /// This is useful for ensuring data is loaded before attempting to scroll to an item.
    /// </summary>
    public static async Task<bool> WaitForNotBusyAsync(Func<bool> isBusyGetter, int maxWaitSeconds = 5, int delayMs = 100, CancellationToken cancellationToken = default)
    {
        if (isBusyGetter == null)
        {
            return false;
        }

        // Check immediately first - if not busy, return immediately
        if (!isBusyGetter())
        {
            return true;
        }

        var maxRetries = (maxWaitSeconds * 1000) / delayMs;

        // Use Polly to retry checking the condition until it becomes false
        var retryPolicy = Policy
            .Handle<InvalidOperationException>() // Throw when still busy, retry
            .WaitAndRetryAsync(
                retryCount: maxRetries,
                sleepDurationProvider: _ => TimeSpan.FromMilliseconds(delayMs),
                onRetry: (_, _, retryCount, _) =>
                {
                    Log.Logger.Debug("Waiting for IsBusy to become false (attempt {RetryCount}/{MaxRetries})",
                        retryCount, maxRetries);
                });

        try
        {
            await retryPolicy.ExecuteAsync(async ct =>
            {
                // Small delay before checking to avoid tight loop
                await Task.Delay(10, ct);
                if (isBusyGetter())
                {
                    throw new InvalidOperationException("Still busy");
                }
            }, cancellationToken);

            // Successfully waited for IsBusy to become false
            return true;
        }
        catch (InvalidOperationException ex)
        {
            // Exhausted retries - still busy after max wait time
            // Polly throws the last handled exception when retries are exhausted
            Log.Logger.Debug(ex, "Timeout waiting for IsBusy to become false");
            return false;
        }
        catch (Exception ex)
        {
            Log.Logger.Debug(ex, "Exception while waiting for IsBusy to become false");
            return false;
        }
    }
}

