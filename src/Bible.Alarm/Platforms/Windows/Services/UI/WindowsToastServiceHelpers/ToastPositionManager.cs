#nullable enable
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Services.UI.ToastLayoutHelpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Serilog;
using Border = Microsoft.UI.Xaml.Controls.Border;
using Window = Microsoft.UI.Xaml.Window;

namespace Bible.Alarm.Platforms.Windows.Services.UI.WindowsToastServiceHelpers;

/// <summary>
/// Manages popup positioning and window size change subscriptions.
/// </summary>
internal static class ToastPositionManager
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

            var bottomMargin = 50 + ToastMiniBarInsetHelper.GetBottomInsetDip();
            popup.HorizontalOffset = ToastPopupOffsets.HorizontalCenterOffset(windowWidth, estimatedPopupWidth);
            popup.VerticalOffset = ToastPopupOffsets.VerticalOffsetAboveBottom(windowHeight, estimatedPopupHeight, bottomMargin);
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
                    Log.Warning(ex, AppConstants.Logging.WindowsToastFlyoutDiagnosticsLog.ExceptionUpdatingPopupPositionOnWindowResize);
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
                Log.Debug(ex, AppConstants.Logging.WindowsToastFlyoutDiagnosticsLog.WindowContentNotAccessibleDuringCleanupUnsubscribe);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, AppConstants.Logging.WindowsToastFlyoutDiagnosticsLog.ExceptionUnsubscribingFromWindowSizeChanged);
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
                    var bottomMargin = 50 + ToastMiniBarInsetHelper.GetBottomInsetDip();
                    popup.HorizontalOffset = ToastPopupOffsets.HorizontalCenterOffset(windowWidth, borderWidth);
                    popup.VerticalOffset = ToastPopupOffsets.VerticalOffsetAboveBottom(windowHeight, borderHeight, bottomMargin);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, AppConstants.Logging.WindowsToastFlyoutDiagnosticsLog.ExceptionUpdatingPopupPosition);
                }
            }
        }
    }
}

