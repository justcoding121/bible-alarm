#nullable enable

using System;
using Bible.Alarm.Services.UI;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using Serilog;

namespace Bible.Alarm.Services.UI.NavigationServiceHelpers;

/// <summary>
/// Handles navigation stack operations like pop.
/// </summary>
public sealed class NavigationStackManager
{
    private const int PopModalRetryDelayMs = 350;

    /// <summary>
    /// Pops the current modal page from the navigation stack.
    /// On WinUI the platform stack can be out of sync with MAUI; we retry once after a short delay.
    /// </summary>
    public async Task PopModalAsync(INavigation navigation)
    {
        if (navigation.ModalStack.Count == 0)
        {
            return;
        }

        var modal = navigation.ModalStack.LastOrDefault();

        async Task TryPopAsync()
        {
            await navigation.PopModalAsync(animated: false);
        }

        try
        {
            await TryPopAsync();
        }
        catch (InvalidOperationException ex)
        {
            Log.Warning(ex, "NavigationStackManager.PopModalAsync: First pop failed (platform stack may be empty), retrying after {Delay}ms", PopModalRetryDelayMs);
            await Task.Delay(PopModalRetryDelayMs);
            try
            {
                await TryPopAsync();
            }
            catch (InvalidOperationException ex2)
            {
                Log.Warning(ex2, "NavigationStackManager.PopModalAsync: Retry pop failed, modal may remain visible. MAUI ModalStack.Count={Count}", navigation.ModalStack.Count);
                return;
            }
        }

        if (modal is IDisposable disposable)
        {
            try
            {
                disposable.Dispose();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "NavigationStackManager.PopModalAsync: Error disposing modal (non-fatal)");
            }
        }

        // Re-apply status bar (and nav bar) so the now-visible page has correct appearance.
        // On iOS, dismissing a modal can leave the status bar in the modal's style (e.g. light content).
        try
        {
            WindowSetupService.UpdateNavigationBarColors();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "NavigationStackManager.PopModalAsync: Error updating navigation bar colors (non-fatal)");
        }

        if (DeviceInfo.Platform == DevicePlatform.WinUI)
        {
            _ = RefreshUnderlyingPageAfterModalPopAsync(navigation);
        }
    }

    private const int WinUIPostPopRefreshDelayMs = 200;

    /// <summary>
    /// WinUI can leave the underlying page with a stale or blank visual after a modal is popped.
    /// After a short delay, force a layout pass on the now-visible page so it repaints.
    /// </summary>
    private static async Task RefreshUnderlyingPageAfterModalPopAsync(INavigation navigation)
    {
        try
        {
            await Task.Delay(WinUIPostPopRefreshDelayMs);
            if (navigation.NavigationStack.Count == 0)
            {
                return;
            }

            var page = navigation.NavigationStack.LastOrDefault();
            if (page != null)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (page is global::Bible.Alarm.Views.Schedule.Schedule schedule)
                        schedule.EnsureContentVisibleAfterModalClosed();
                    else
                        page.InvalidateMeasure();

                    // Structural kick: toggle visibility on native WinUI root to force recomposition
                    ForceNativeRecomposition();
                });
            }
        }
        catch (Exception)
        {
            // Refresh is best-effort; ignore failure.
        }
    }

    /// <summary>
    /// Toggles the native WinUI window content visibility to force a full visual recomposition.
    /// This is a last-resort workaround for WinUI leaving the visual tree blank after modal pop.
    /// </summary>
    private static void ForceNativeRecomposition()
    {
#if WINDOWS
        try
        {
            var nativeWindow = Application.Current?.Windows?.FirstOrDefault()
                ?.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
            if (nativeWindow?.Content is Microsoft.UI.Xaml.UIElement rootElement)
            {
                rootElement.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                rootElement.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
                if (rootElement is Microsoft.UI.Xaml.FrameworkElement fe)
                    fe.UpdateLayout();
            }
        }
        catch (Exception)
        {
            // Best-effort; ignore.
        }
#endif
    }

    /// <summary>
    /// Pops the current page from the navigation stack.
    /// </summary>
    public async Task PopAsync(INavigation navigation)
    {
        if (navigation.NavigationStack.Count <= 1)
        {
            return;
        }

        var page = navigation.NavigationStack.LastOrDefault();

        try
        {
            await navigation.PopAsync(animated: true);
        }
        catch (Exception ex) when (IsAndroidNavControllerError(ex))
        {
            Log.Warning(ex, "NavigationStackManager.PopAsync: NavController back stack out of sync with MAUI navigation stack");
            return;
        }

        // Dispose the page if it implements IDisposable
        if (page is IDisposable disposable)
        {
            try
            {
                disposable.Dispose();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "NavigationStackManager.PopAsync: Error disposing page (non-fatal)");
            }
        }

        // Re-apply status bar so the now-visible page has correct appearance (same as after modal pop).
        try
        {
            WindowSetupService.UpdateNavigationBarColors();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "NavigationStackManager.PopAsync: Error updating navigation bar colors (non-fatal)");
        }
    }

    /// <summary>
    /// Detects Android NavController back stack errors that occur when MAUI's managed
    /// navigation stack is out of sync with the native Jetpack NavController back stack.
    /// </summary>
    internal static bool IsAndroidNavControllerError(Exception ex)
    {
#if ANDROID
        return ex is Java.Lang.IllegalArgumentException
            && ex.Message?.Contains("NavController's back stack") == true;
#else
        return false;
#endif
    }
}
