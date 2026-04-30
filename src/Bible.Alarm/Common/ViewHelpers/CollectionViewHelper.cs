#nullable enable
using System.Collections;
using MauiCollectionView = Microsoft.Maui.Controls.CollectionView;
#if WINDOWS
using Microsoft.Maui.Essentials;
#endif

namespace Bible.Alarm.Common.ViewHelpers;

/// <summary>
/// Main helper class for CollectionView scrolling operations.
/// Delegates to specialized helper classes for modularity.
/// </summary>
public static class CollectionViewHelper
{
    /// <summary>
    /// Waits for the CollectionView to be ready and then scrolls to the specified item.
    /// On Windows, this waits for the visual tree to be fully loaded before scrolling.
    /// Uses production-ready scrolling logic: for last items, finds parent ScrollView and scrolls to content height.
    /// Falls back to CollectionView.ScrollTo for other cases with platform-specific optimizations.
    /// For large lists (100+ items), uses index-based scrolling for better reliability with virtualized CollectionViews.
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

            var isReady = await CollectionViewReadinessChecker.WaitForCollectionViewReadyAsync(collectionView, item, cancellationToken);
            if (!isReady)
            {
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();

            var canScroll = await CanSafelyScrollAsync(collectionView, cancellationToken);
            if (!canScroll)
            {
                return;
            }

            // For large lists, add extra delay to allow virtualization to settle
            var itemsSource = collectionView.ItemsSource;
            var itemCount = GetItemCount(itemsSource);

            if (itemCount > 100)
            {
                // Extra delay for large lists - virtualization needs more time
                var extraDelay = Math.Min((itemCount / 100) * 50, 300); // 50ms per 100 items, max 300ms
                await Task.Delay(extraDelay, cancellationToken);
            }

            // Determine the best scroll position based on item location in the list
            var finalPosition = CollectionViewScrollPositionHelper.DetermineOptimalScrollPosition(collectionView, item, position);

            await CollectionViewScrollExecutor.PerformScrollAsync(collectionView, item, finalPosition, animated, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Cancellation requested - this is expected, don't log
        }
        catch (Exception)
        {
            // Ignore errors - scrolling is not critical
        }
    }

    private static int GetItemCount(object? itemsSource)
    {
        if (itemsSource == null) return 0;

        if (itemsSource is ICollection collection)
            return collection.Count;

        if (itemsSource is IEnumerable enumerable)
            return enumerable.Cast<object>().Count();

        return 0;
    }

    private static async Task<bool> CanSafelyScrollAsync(MauiCollectionView collectionView, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(collectionView);
        cancellationToken.ThrowIfCancellationRequested();
#if WINDOWS
        if (DeviceInfo.Platform == DevicePlatform.WinUI)
        {
            return await WindowsNativeScrollHelper.CanSafelyScrollWindows(collectionView, cancellationToken);
        }
#endif
        return true;
    }

    /// <summary>
    /// Waits for a ViewModel's IsBusy property to become false by polling. Does not throw;
    /// returns false only when the timeout is exhausted. Avoids flooding debug output with exceptions.
    /// </summary>
    public static async Task<bool> WaitForNotBusyAsync(Func<bool> isBusyGetter, int maxWaitSeconds = 10, int delayMs = 100, CancellationToken cancellationToken = default)
    {
        if (isBusyGetter == null)
        {
            return false;
        }

        if (!isBusyGetter())
        {
            return true;
        }

        var deadline = DateTime.UtcNow.AddSeconds(maxWaitSeconds);

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(delayMs, cancellationToken);
            if (!isBusyGetter())
            {
                return true;
            }
        }

        return false;
    }
}

