#nullable enable

using System;
using Bible.Alarm.Services.UI;
using Bible.Alarm.Shared.Constants;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using Serilog;
#if IOS
using UIKit;
using CoreAnimation;
using Bible.Alarm.Platforms.iOS.Helpers;
#endif

namespace Bible.Alarm.Services.UI.NavigationServiceHelpers;

/// <summary>
/// Handles navigation stack operations like pop.
/// </summary>
public static class NavigationStackManager
{
    private const int PopModalRetryDelayMs = 350;

    /// <summary>
    /// Pops the current modal page from the navigation stack.
    /// On WinUI the platform stack can be out of sync with MAUI; we retry once after a short delay.
    /// </summary>
    public static async Task PopModalAsync(INavigation navigation)
    {
        if (navigation.ModalStack.Count == 0)
        {
            return;
        }

        var modal = navigation.ModalStack[^1];

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
            Log.Warning(ex, AppConstants.Logging.NavigationStackManagerDiagnosticsLog.PopModalAsyncFirstPopFailedRetry, PopModalRetryDelayMs);
            await Task.Delay(PopModalRetryDelayMs);
            try
            {
                await TryPopAsync();
            }
            catch (InvalidOperationException ex2)
            {
                Log.Warning(ex2, AppConstants.Logging.NavigationStackManagerDiagnosticsLog.PopModalAsyncRetryPopFailed, navigation.ModalStack.Count);
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
                Log.Warning(ex, AppConstants.Logging.NavigationStackManagerDiagnosticsLog.PopModalAsyncErrorDisposingModalNonFatal);
            }
        }

#if IOS
        if (modal != null)
        {
            CleanupIOSNativeViews(modal);
        }
#endif

        // Re-apply status bar (and nav bar) so the now-visible page has correct appearance.
        // On iOS, dismissing a modal can leave the status bar in the modal's style (e.g. light content).
        try
        {
            WindowSetupService.UpdateNavigationBarColors();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, AppConstants.Logging.NavigationStackManagerDiagnosticsLog.PopModalAsyncErrorUpdatingNavigationBarColorsNonFatal);
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

            var page = navigation.NavigationStack[^1];
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
        catch (Exception ex)
        {
            Log.Warning(ex, AppConstants.Logging.NavigationStackManagerDiagnosticsLog.RefreshAfterModalPopFailedBestEffort);
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
            var nativeWindow = Application.Current?.Windows is { Count: > 0 } wins
                ? wins[0].Handler?.PlatformView as Microsoft.UI.Xaml.Window
                : null;
            if (nativeWindow?.Content is Microsoft.UI.Xaml.UIElement rootElement)
            {
                rootElement.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                rootElement.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
                if (rootElement is Microsoft.UI.Xaml.FrameworkElement fe)
                    fe.UpdateLayout();
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, AppConstants.Logging.NavigationStackManagerDiagnosticsLog.ForceNativeRecompositionFailedBestEffort);
        }
#endif
    }

    /// <summary>
    /// Pops all modals from the navigation stack (top to bottom), disposing each.
    /// </summary>
    public static async Task PopAllModalsAsync(INavigation navigation)
    {
        while (navigation.ModalStack.Count > 0)
        {
            await PopModalAsync(navigation);
        }
    }

    /// <summary>
    /// Pops the current page from the navigation stack.
    /// </summary>
    public static async Task PopAsync(INavigation navigation)
    {
        if (navigation.NavigationStack.Count <= 1)
        {
            return;
        }

        var page = navigation.NavigationStack[^1];

        if (page is Views.Home)
        {
            Log.Warning(AppConstants.Logging.NavigationStackManagerDiagnosticsLog.PopAsyncRefusingPopHome);
            return;
        }

        try
        {
            await navigation.PopAsync(animated: true);
        }
        catch (Exception ex) when (IsAndroidNavControllerError(ex))
        {
            Log.Warning(ex, AppConstants.Logging.NavigationStackManagerDiagnosticsLog.PopAsyncNavControllerBackStackOutOfSync);
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
                Log.Warning(ex, AppConstants.Logging.NavigationStackManagerDiagnosticsLog.PopAsyncErrorDisposingPageNonFatal);
            }
        }

#if IOS
        if (page != null)
        {
            CleanupIOSNativeViews(page);
        }
#endif

        // Re-apply status bar so the now-visible page has correct appearance (same as after modal pop).
        try
        {
            WindowSetupService.UpdateNavigationBarColors();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, AppConstants.Logging.NavigationStackManagerDiagnosticsLog.PopAsyncErrorUpdatingNavigationBarColorsNonFatal);
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
            && ex.Message?.Contains("NavController's back stack") is true;
#else
        return false;
#endif
    }

#if IOS
    /// <summary>
    /// Two-phase cleanup for popped iOS pages:
    /// 1. Collect native UIView references from the MAUI visual tree (must happen BEFORE
    ///    DisconnectHandler nulls out Handler.PlatformView).
    /// 2. Disconnect MAUI handlers (releases managed-to-native bindings, calls
    ///    GC.SuppressFinalize on handler-owned objects).
    /// 3. Walk the collected native views' CALayer hierarchy and suppress finalization
    ///    on every layer. This catches objects like StaticCAShapeLayer that
    ///    DisconnectHandler does NOT clean up — preventing the GC finalizer from
    ///    sending objc_msgSend to already-deallocated native objects (SIGSEGV).
    /// </summary>
    internal static void CleanupIOSNativeViews(IVisualTreeElement element)
    {
        var nativeViews = new List<UIView>();
        CollectNativeViews(element, nativeViews);

        DisconnectHandlersRecursively(element);

        foreach (var view in nativeViews)
        {
            try
            {
                IOSNativeViewCleanupHelper.SuppressFinalizersForViewHierarchy(view);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, AppConstants.Logging.NavigationStackManagerDiagnosticsLog.CleanupIOSNativeViewsErrorSuppressingFinalizersForTypeNonFatal, view.GetType().Name);
            }
        }
    }

    private static void CollectNativeViews(IVisualTreeElement element, List<UIView> views)
    {
        foreach (var child in element.GetVisualChildren())
        {
            CollectNativeViews(child, views);
        }

        if (element is not IElement mauiElement)
        {
            return;
        }

        var handler = mauiElement.Handler;
        if (handler == null)
        {
            return;
        }

        if (handler.PlatformView is UIView view)
        {
            views.Add(view);
        }

        if (handler is not IPlatformViewHandler pvh)
        {
            return;
        }

        try
        {
            var vc = pvh.ViewController;
            if (vc?.View is UIView vcView && vcView != handler.PlatformView)
            {
                views.Add(vcView);
            }
        }
        catch (ObjectDisposedException ex)
        {
            Log.Debug(ex, AppConstants.Logging.NavigationStackManagerDiagnosticsLog.CollectNativeViewsViewControllerAccessDisposedNonFatal);
        }
    }

    private static void DisconnectHandlersRecursively(IVisualTreeElement element)
    {
        foreach (var child in element.GetVisualChildren())
        {
            DisconnectHandlersRecursively(child);
        }

        try
        {
            if (element is IElement mauiElement)
            {
                var handler = mauiElement.Handler;
                if (handler == null)
                {
                    return;
                }

                handler.DisconnectHandler();

                // After disconnection the handler's native references are released by iOS.
                // Suppress the managed finalizer so it won't send objc_msgSend to freed objects.
                GC.SuppressFinalize(handler);

                if (handler is IPlatformViewHandler pvh)
                {
                    SuppressViewControllerFinalizer(pvh);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, AppConstants.Logging.NavigationStackManagerDiagnosticsLog.DisconnectHandlersRecursivelyErrorDisconnectingHandlerForTypeNonFatal, element.GetType().Name);
        }
    }

    private static void SuppressViewControllerFinalizer(IPlatformViewHandler pvh)
    {
        try
        {
            var vc = pvh.ViewController;
            if (vc == null)
            {
                return;
            }

            GC.SuppressFinalize(vc);

            if (vc.View != null)
            {
                IOSNativeViewCleanupHelper.SuppressFinalizersForViewHierarchy(vc.View);
            }
        }
        catch (ObjectDisposedException ex)
        {
            Log.Debug(ex, AppConstants.Logging.NavigationStackManagerDiagnosticsLog.SuppressViewControllerFinalizerViewControllerDisposedNonFatal);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, AppConstants.Logging.NavigationStackManagerDiagnosticsLog.SuppressViewControllerFinalizerErrorNonFatal);
        }
    }

#endif
}
