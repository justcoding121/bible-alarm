#nullable enable
using System.Collections;
using Polly;
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
        catch
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
#if WINDOWS
        if (DeviceInfo.Platform == DevicePlatform.WinUI)
        {
            return await WindowsNativeScrollHelper.CanSafelyScrollWindows(collectionView, cancellationToken);
        }
#endif
        return true;
    }

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
                sleepDurationProvider: _ => TimeSpan.FromMilliseconds(delayMs));

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
        catch
        {
            // Exhausted retries - still busy after max wait time
            return false;
        }
    }
}

