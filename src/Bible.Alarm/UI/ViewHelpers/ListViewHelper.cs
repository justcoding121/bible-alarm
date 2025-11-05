using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
#if WINDOWS
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinUIListView = Microsoft.UI.Xaml.Controls.ListView;
#endif
using MauiListView = Microsoft.Maui.Controls.ListView;

namespace Bible.Alarm.UI.ViewHelpers;

public static class ListViewHelper
{
    /// <summary>
    /// Waits for the ListView to be ready and then scrolls to the specified item.
    /// On Windows, this waits for the visual tree to be fully loaded before scrolling.
    /// </summary>
    public static async Task ScrollToWhenReadyAsync(MauiListView listView, object item, ScrollToPosition position = ScrollToPosition.Center, bool animated = true)
    {
        if (listView == null || item == null)
            return;

        try
        {
            // Wait for the ListView to be loaded
            if (DeviceInfo.Platform == DevicePlatform.WinUI)
            {
                // On Windows, wait longer and check if the visual tree is ready
                await WaitForListViewReadyWindows(listView);
            }
            else
            {
                // On other platforms, a shorter delay is usually sufficient
                await Task.Delay(200);
            }

            // Check if ListView is still valid and has items
            if (listView.ItemsSource != null)
            {
                // Additional verification on Windows before attempting scroll
                bool canScroll = true;
#if WINDOWS
                if (DeviceInfo.Platform == DevicePlatform.WinUI)
                {
                    canScroll = await CanSafelyScrollWindows(listView);
                }
#endif
                
                if (canScroll)
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        try
                        {
                            listView.ScrollTo(item, position, animated);
                        }
                        catch (System.Runtime.InteropServices.COMException)
                        {
                            // Visual tree/ScrollViewer not ready yet, ignore the error
                        }
                        catch
                        {
                            // Other errors, ignore
                        }
                    });
                }
            }
        }
        catch
        {
            // Ignore errors - scrolling is not critical
        }
    }

    private static async Task WaitForListViewReadyWindows(MauiListView listView)
    {
        const int maxAttempts = 50; // Maximum number of attempts (5 seconds total)
        const int delayMs = 100; // Delay between attempts
        bool isReady = false;

        for (int i = 0; i < maxAttempts; i++)
        {
            await Task.Delay(delayMs);
            
            try
            {
                // Check if ListView has items
                if (listView.ItemsSource == null)
                    continue;

                // Check if handler is available
                if (listView.Handler == null)
                    continue;

#if WINDOWS
                // Check if the native control is loaded
                if (listView.Handler.PlatformView is Microsoft.UI.Xaml.FrameworkElement frameworkElement)
                {
                    // Check if IsLoaded property is true (safer than accessing visual tree)
                    if (frameworkElement.IsLoaded)
                    {
                        // Check if ItemsSourceView is accessible and has items
                        if (frameworkElement is ItemsControl itemsControl)
                        {
                            try
                            {
                                // Check if ItemsSource is set and Items.Count > 0
                                // ItemsControl.ItemsSourceView is not directly accessible,
                                // but we can check Items.Count as an alternative
                                var items = itemsControl.Items;
                                if (items != null && items.Count > 0)
                                {
                                    // Wait longer for the ScrollViewer to be ready
                                    // The ScrollViewer is what needs to be ready for scrolling to work
                                    await Task.Delay(500);
                                    isReady = true;
                                    break;
                                }
                            }
                            catch
                            {
                                // Items not ready yet, continue waiting
                                continue;
                            }
                        }
                        else
                        {
                            // Control is loaded, wait longer for rendering and ScrollViewer initialization
                            await Task.Delay(500);
                            isReady = true;
                            break;
                        }
                    }
                }
#else
                // On other platforms, if handler exists, we're likely ready
                if (listView.Handler.PlatformView != null)
                {
                    return;
                }
#endif
            }
            catch
            {
                // Visual tree not ready yet, continue waiting
            }
        }

        // Additional wait to ensure ScrollViewer is ready
        if (isReady)
        {
            await Task.Delay(300);
        }
    }

#if WINDOWS
    private static async Task<bool> CanSafelyScrollWindows(MauiListView listView)
    {
        try
        {
            if (listView.Handler?.PlatformView is Microsoft.UI.Xaml.FrameworkElement frameworkElement)
            {
                if (!frameworkElement.IsLoaded)
                    return false;

                // Try to verify the ScrollViewer exists without accessing the visual tree directly
                // We'll check if we can safely access the ItemsControl properties
                if (frameworkElement is ItemsControl itemsControl)
                {
                    // Check if Items collection is accessible and has items
                    var items = itemsControl.Items;
                    if (items == null || items.Count == 0)
                        return false;

                    // Wait longer to ensure ScrollViewer is fully initialized
                    // The ScrollViewer needs time to be created and added to the visual tree
                    await Task.Delay(400);
                    return true;
                }
            }
        }
        catch
        {
            // Can't safely scroll yet
            return false;
        }

        return false;
    }
#endif
}

