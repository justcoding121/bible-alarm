#nullable enable
using Bible.Alarm.Shared.Helpers;
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
        if (CollectionViewItemsSourceWarmupGate.ShouldYieldOnceBeforePolling(collectionView.ItemsSource, item))
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
        catch (Exception)
        {
            return false;
        }
    }

    private static bool IsCollectionViewValid(MauiCollectionView collectionView, object item)
    {
        return CollectionViewValidityPrecheck.HasRenderableItemsSourceWithHandler(
            collectionView.ItemsSource,
            collectionView.Handler != null,
            item);
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
}
