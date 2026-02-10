#nullable enable

using System;
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
            disposable.Dispose();
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
        await navigation.PopAsync(animated: true);

        // Dispose the page if it implements IDisposable
        if (page is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
