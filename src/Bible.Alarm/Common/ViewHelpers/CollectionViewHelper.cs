#if WINDOWS
using Microsoft.UI.Xaml;
#endif
using System.Collections;
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
    /// </summary>
    public static async Task ScrollToWhenReadyAsync(MauiCollectionView collectionView, object item, ScrollToPosition position = ScrollToPosition.Center, bool animated = false, CancellationToken cancellationToken = default)
    {
        if (collectionView == null || item == null)
        {
            return;
        }

        try
        {
            // Check cancellation token before starting
            cancellationToken.ThrowIfCancellationRequested();

            // Wait for the CollectionView to be ready on all platforms
            var isReady = await WaitForCollectionViewReadyAsync(collectionView, item, cancellationToken);

            if (!isReady)
            {
                Log.Logger.Debug("CollectionView not ready for scrolling or item not found in ItemsSource");
                return;
            }

            // Check cancellation token again after waiting
            cancellationToken.ThrowIfCancellationRequested();

            // Additional verification on Windows before attempting scroll
            var canScroll = true;
#if WINDOWS
            if (DeviceInfo.Platform == DevicePlatform.WinUI)
            {
                canScroll = await CanSafelyScrollWindows(collectionView, cancellationToken);
            }
#endif

            if (canScroll)
            {
                // Wrap in Task.Run to catch async exceptions that might escape
                await Task.Run(async () =>
                {
                    try
                    {
                        await MainThread.InvokeOnMainThreadAsync(() =>
                        {
                            try
                            {
                                // Multiple checks to ensure CollectionView is still valid
                                if (collectionView == null)
                                {
                                    Log.Logger.Debug("CollectionView is null, skipping scroll");
                                    return;
                                }

                                // Check if CollectionView is still attached to a parent (not disposed)
                                if (collectionView.Parent == null)
                                {
                                    Log.Logger.Debug("CollectionView is not attached to parent, skipping scroll");
                                    return;
                                }

                                // Double-check handler is still valid before scrolling
                                // MAUI's ScrollTo internally accesses handler.ItemCount which can throw NullReferenceException
                                if (collectionView.Handler == null || collectionView.Handler.PlatformView == null)
                                {
                                    Log.Logger.Debug("CollectionView handler is null or disposed, skipping scroll");
                                    return;
                                }

                                // Check cancellation token one more time
                                cancellationToken.ThrowIfCancellationRequested();

                                collectionView.ScrollTo(item, position: position, animate: animated);
                                Log.Logger.Debug("Successfully scrolled to item in CollectionView");
                            }
                            catch (OperationCanceledException)
                            {
                                // Cancellation requested - this is expected, don't log
                            }
                            catch (NullReferenceException ex)
                            {
                                // Handler was disposed or ItemCount property is null - ignore the error
                                Log.Logger.Debug(ex, "NullReferenceException while scrolling CollectionView - handler may be disposed");
                            }
                            catch (COMException ex)
                            {
                                // Visual tree/ScrollViewer not ready yet, ignore the error
                                Log.Logger.Debug(ex, "COMException while scrolling CollectionView - visual tree not ready yet");
                            }
                            catch (Exception ex)
                            {
                                // Other errors, log but don't throw
                                Log.Logger.Debug(ex, "Exception while scrolling CollectionView: {Message}", ex.Message);
                            }
                        });
                    }
                    catch (OperationCanceledException)
                    {
                        // Cancellation requested - this is expected, don't log
                    }
                    catch (Exception ex)
                    {
                        // Catch any async exceptions that escape
                        Log.Logger.Debug(ex, "Exception in async scroll operation: {Message}", ex.Message);
                    }
                }, cancellationToken);
            }
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

    private static async Task<bool> WaitForCollectionViewReadyAsync(MauiCollectionView collectionView, object item, CancellationToken cancellationToken = default)
    {
        // Maximum number of attempts (5 seconds total)
        const int MaxAttempts = 50;
        // Delay between attempts
        const int DelayMs = 100;

        for (var i = 0; i < MaxAttempts; i++)
        {
            // Check cancellation before each delay
            cancellationToken.ThrowIfCancellationRequested();

            await Task.Delay(DelayMs, cancellationToken);

            try
            {
                // Check if CollectionView has items
                if (collectionView.ItemsSource == null)
                {
                    continue;
                }

                // Check if ItemsSource has any items
                if (!HasItems(collectionView.ItemsSource))
                {
                    continue;
                }

                // Check if handler is available
                if (collectionView.Handler == null)
                {
                    continue;
                }

                // Verify item exists in ItemsSource
                if (!ItemExistsInSource(collectionView.ItemsSource, item))
                {
                    continue;
                }

#if WINDOWS
                if (DeviceInfo.Platform == DevicePlatform.WinUI)
                {
                    // Check if the native control is loaded
                    if (collectionView.Handler.PlatformView is FrameworkElement frameworkElement)
                    {
                        // Check if IsLoaded property is true (safer than accessing visual tree)
                        if (frameworkElement.IsLoaded)
                        {
                            // Wait longer for the ScrollViewer to be ready
                            await Task.Delay(300, cancellationToken);
                            return true;
                        }
                    }
                }
                else
#endif
                {
                    // On other platforms, if handler exists and item is found, we're ready
                    if (collectionView.Handler.PlatformView != null)
                    {
                        // Additional small delay to ensure rendering is complete
                        await Task.Delay(200, cancellationToken);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                // Visual tree not ready yet, continue waiting
                Log.Logger.Debug(ex, "Exception while waiting for CollectionView ready - visual tree not ready yet");
            }
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
                foreach (var _ in enumerable)
                {
                    // At least one item exists
                    return true;
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
                onRetry: (exception, timeSpan, retryCount, context) =>
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

