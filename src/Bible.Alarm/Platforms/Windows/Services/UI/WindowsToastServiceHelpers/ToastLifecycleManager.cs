#nullable enable
using Bible.Alarm.Shared.Constants;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Serilog;
using Window = Microsoft.UI.Xaml.Window;

namespace Bible.Alarm.Platforms.Windows.Services.UI.WindowsToastServiceHelpers;

/// <summary>
/// Manages toast popup lifecycle (cleanup, closing, etc.).
/// </summary>
internal sealed class ToastLifecycleManager
{
    public static async Task CloseExistingPopupIfNeededAsync(Popup? currentPopup)
    {
        if (currentPopup != null)
        {
            try
            {
                currentPopup.IsOpen = false;
                await Task.Delay(100);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, AppConstants.Logging.WindowsToastFlyoutDiagnosticsLog.ExceptionClosingExistingPopupBeforeShowingNew);
            }
            finally
            {
                CleanupPopup(currentPopup);
            }
        }
    }

    public static void CleanupPopup(Popup? popup)
    {
        if (popup == null)
        {
            return;
        }

        try
        {
            popup.Child = null;
        }
        catch (System.Runtime.InteropServices.COMException ex)
        {
            Log.Debug(ex, AppConstants.Logging.WindowsToastFlyoutDiagnosticsLog.PopupNotAccessibleDuringCleanup);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, AppConstants.Logging.WindowsToastFlyoutDiagnosticsLog.ExceptionClearingPopupChild);
        }
    }

    public static async Task CleanupFlyoutResources(Window? currentWindow, Popup popup)
    {
        ToastPositionManager.UnsubscribeFromWindowSizeChanges(currentWindow);

        try
        {
            if (popup.IsOpen)
            {
                popup.IsOpen = false;
            }
        }
        catch (System.Runtime.InteropServices.COMException ex)
        {
            Log.Debug(ex, AppConstants.Logging.WindowsToastFlyoutDiagnosticsLog.PopupNotAccessibleDuringCleanup);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, AppConstants.Logging.WindowsToastFlyoutDiagnosticsLog.ExceptionClosingPopupInShowFlyoutFinally);
        }

        await Task.Delay(200);
        CleanupPopup(popup);
    }

    public static FrameworkElement? SetupPopupAndGetWindowContent(Popup popup, Window currentWindow)
    {
        try
        {
            if (currentWindow?.Content is FrameworkElement content)
            {
                if (content.XamlRoot != null)
                {
                    popup.XamlRoot = content.XamlRoot;
                }
                return content;
            }
        }
        catch (System.Runtime.InteropServices.COMException ex)
        {
            Log.Debug(ex, AppConstants.Logging.WindowsToastFlyoutDiagnosticsLog.WindowContentNotAccessibleWhenSettingUpPopup);
        }

        return null;
    }
}

