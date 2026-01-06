#nullable enable
#if WINDOWS
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Controls;
using Serilog;
using Microsoft.Maui.Controls;
using MauiCollectionView = Microsoft.Maui.Controls.CollectionView;

namespace Bible.Alarm.Common.ViewHelpers;

/// <summary>
/// Helper class for Windows-specific native ScrollViewer scrolling operations.
/// </summary>
internal static class WindowsNativeScrollHelper
{
    /// <summary>
    /// Finds the native ScrollViewer in the CollectionView's visual tree and scrolls it to the bottom.
    /// This simulates scrolling to the maximum extent, similar to mouse wheel scrolling.
    /// </summary>
    public static async Task<bool> ScrollToBottomUsingNativeScrollViewer(MauiCollectionView collectionView, CancellationToken cancellationToken)
    {
        try
        {
            if (collectionView.Handler?.PlatformView is not FrameworkElement platformView)
            {
                return false;
            }

            // Wait for the visual tree to be fully loaded
            await Task.Delay(200, cancellationToken);

            // Find the ScrollViewer in the visual tree
            ScrollViewer? scrollViewer = FindScrollViewer(platformView);
            if (scrollViewer == null)
            {
                return false;
            }

            // Wait for the ScrollViewer to have a valid extent
            int attempts = 0;
            while (scrollViewer.ExtentHeight <= 0 && attempts < 20)
            {
                await Task.Delay(50, cancellationToken);
                attempts++;
            }

            if (scrollViewer.ExtentHeight <= 0)
            {
                return false;
            }

            // Scroll to the maximum vertical offset (bottom)
            var maxOffset = scrollViewer.ExtentHeight - scrollViewer.ViewportHeight;
            scrollViewer.ChangeView(null, maxOffset, null, disableAnimation: true);

            // Wait for scroll to complete
            await Task.Delay(100, cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Recursively finds the ScrollViewer in the visual tree.
    /// </summary>
    private static ScrollViewer? FindScrollViewer(DependencyObject element)
    {
        if (element == null)
        {
            return null;
        }

        if (element is ScrollViewer scrollViewer)
        {
            return scrollViewer;
        }

        int childCount = VisualTreeHelper.GetChildrenCount(element);
        for (int i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(element, i);
            var result = FindScrollViewer(child);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    /// <summary>
    /// Checks if it's safe to scroll on Windows by verifying the visual tree is loaded.
    /// </summary>
    public static async Task<bool> CanSafelyScrollWindows(MauiCollectionView collectionView, CancellationToken cancellationToken = default)
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
        catch
        {
            return false;
        }

        return false;
    }

    /// <summary>
    /// Checks if the CollectionView is ready for scrolling on Windows.
    /// </summary>
    public static async Task<bool> CheckWindowsReady(MauiCollectionView collectionView, CancellationToken cancellationToken)
    {
        if (collectionView.Handler?.PlatformView is FrameworkElement frameworkElement && frameworkElement.IsLoaded)
        {
            await Task.Delay(300, cancellationToken);
            return true;
        }
        return false;
    }
}
#endif
