#nullable enable
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Serilog;

namespace Bible.Alarm.Platforms.Windows.Services.UI.WindowsToastServiceHelpers;

/// <summary>
/// Manages popup positioning and window size change subscriptions.
/// </summary>
internal sealed class ToastPositionManager
{
    private static SizeChangedEventHandler? sizeChangedHandler;

    public static void SetInitialPopupPosition(Popup popup, FrameworkElement? windowContent)
    {
        if (windowContent != null)
        {
            var windowWidth = windowContent.ActualWidth > 0 ? windowContent.ActualWidth : 400;
            var windowHeight = windowContent.ActualHeight > 0 ? windowContent.ActualHeight : 600;
            var estimatedPopupWidth = 300;
            var estimatedPopupHeight = 50;

            popup.HorizontalOffset = (windowWidth - estimatedPopupWidth) / 2;
            popup.VerticalOffset = windowHeight - estimatedPopupHeight - 50;
        }
    }

    public static void SubscribeToWindowSizeChanges(FrameworkElement? windowContent, Popup popup, Window currentWindow)
    {
        if (windowContent != null)
        {
            sizeChangedHandler = (_, _) =>
            {
                try
                {
                    UpdatePopupPosition(popup, currentWindow);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Exception occurred while updating popup position on window resize");
                }
            };
            windowContent.SizeChanged += sizeChangedHandler;
        }
    }

    public static void UnsubscribeFromWindowSizeChanges(Window? currentWindow)
    {
        if (sizeChangedHandler != null)
        {
            try
            {
                if (currentWindow?.Content is FrameworkElement content)
                {
                    content.SizeChanged -= sizeChangedHandler;
                }
            }
            catch (System.Runtime.InteropServices.COMException ex)
            {
                Log.Debug(ex, "Window content not accessible during cleanup (window may be disposed)");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Exception occurred while unsubscribing from window size changed event");
            }
            finally
            {
                sizeChangedHandler = null;
            }
        }
    }

    public static void UpdatePopupPosition(Popup popup, Window? currentWindow)
    {
        if (currentWindow?.Content is FrameworkElement content && popup.Child is Border border)
        {
            var windowWidth = content.ActualWidth;
            var windowHeight = content.ActualHeight;
            var borderWidth = border.ActualWidth;
            var borderHeight = border.ActualHeight;

            if (windowWidth > 0 && windowHeight > 0 && borderWidth > 0 && borderHeight > 0)
            {
                try
                {
                    popup.HorizontalOffset = (windowWidth - borderWidth) / 2;
                    popup.VerticalOffset = windowHeight - borderHeight - 50;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Exception occurred while updating popup position");
                }
            }
        }
    }
}

