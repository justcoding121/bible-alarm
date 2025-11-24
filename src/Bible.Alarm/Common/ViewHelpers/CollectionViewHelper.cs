#if WINDOWS
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
#endif
using System.Runtime.InteropServices;
using Serilog;
using MauiCollectionView = Microsoft.Maui.Controls.CollectionView;

namespace Bible.Alarm.Common.ViewHelpers;

public static class CollectionViewHelper
{
    /// <summary>
    /// Waits for the CollectionView to be ready and then scrolls to the specified item.
    /// On Windows, this waits for the visual tree to be fully loaded before scrolling.
    /// </summary>
    public static async Task ScrollToWhenReadyAsync(MauiCollectionView collectionView, object item, ScrollToPosition position = ScrollToPosition.Center, bool animated = true)
    {
        if (collectionView == null || item == null)
            return;

        try
        {
            // Wait for the CollectionView to be loaded
            if (DeviceInfo.Platform == DevicePlatform.WinUI)
            {
                // On Windows, wait longer and check if the visual tree is ready
                await WaitForCollectionViewReadyWindows(collectionView);
            }
            else
            {
                // On other platforms, a shorter delay is usually sufficient
                await Task.Delay(200);
            }

            // Check if CollectionView is still valid and has items
            if (collectionView.ItemsSource != null)
            {
                // Additional verification on Windows before attempting scroll
                var canScroll = true;
#if WINDOWS
                if (DeviceInfo.Platform == DevicePlatform.WinUI)
                {
                    canScroll = await CanSafelyScrollWindows(collectionView);
                }
#endif
                
                if (canScroll)
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        try
                        {
                            collectionView.ScrollTo(item, position: position, animate: animated);
                        }
                        catch (COMException ex)
                        {
                            // Visual tree/ScrollViewer not ready yet, ignore the error
                            Log.Logger.Debug(ex, "COMException while scrolling CollectionView - visual tree not ready yet");
                        }
                        catch (Exception ex)
                        {
                            // Other errors, ignore
                            Log.Logger.Debug(ex, "Exception while scrolling CollectionView");
                        }
                    });
                }
            }
        }
        catch (Exception ex)
        {
            // Ignore errors - scrolling is not critical
            Log.Logger.Debug(ex, "Exception in ScrollToWhenReadyAsync - scrolling is not critical");
        }
    }

    private static async Task WaitForCollectionViewReadyWindows(MauiCollectionView collectionView)
    {
        const int maxAttempts = 50; // Maximum number of attempts (5 seconds total)
        const int delayMs = 100; // Delay between attempts
        var isReady = false;

        for (var i = 0; i < maxAttempts; i++)
        {
            await Task.Delay(delayMs);
            
            try
            {
                // Check if CollectionView has items
                if (collectionView.ItemsSource == null)
                    continue;

                // Check if handler is available
                if (collectionView.Handler == null)
                    continue;

#if WINDOWS
                // Check if the native control is loaded
                if (collectionView.Handler.PlatformView is FrameworkElement frameworkElement)
                {
                    // Check if IsLoaded property is true (safer than accessing visual tree)
                    if (frameworkElement.IsLoaded)
                    {
                        // Wait longer for the ScrollViewer to be ready
                        // The ScrollViewer is what needs to be ready for scrolling to work
                        await Task.Delay(500);
                        isReady = true;
                        break;
                    }
                }
#else
                // On other platforms, if handler exists, we're likely ready
                if (collectionView.Handler.PlatformView != null)
                {
                    return;
                }
#endif
            }
            catch (Exception ex)
            {
                // Visual tree not ready yet, continue waiting
                Log.Logger.Debug(ex, "Exception while waiting for CollectionView ready - visual tree not ready yet");
            }
        }

        // Additional wait to ensure ScrollViewer is ready
        if (isReady)
        {
            await Task.Delay(300);
        }
    }

#if WINDOWS
    private static async Task<bool> CanSafelyScrollWindows(MauiCollectionView collectionView)
    {
        try
        {
            if (collectionView.Handler?.PlatformView is FrameworkElement frameworkElement)
            {
                if (!frameworkElement.IsLoaded)
                    return false;

                // Wait longer to ensure ScrollViewer is fully initialized
                // The ScrollViewer needs time to be created and added to the visual tree
                await Task.Delay(400);
                return true;
            }
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
}

