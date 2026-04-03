#nullable enable

using System.Runtime.InteropServices;
using Bible.Alarm.Platforms.Windows.Services.UI.WindowsToastServiceHelpers;
using Bible.Alarm.Services.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Serilog;
using Window = Microsoft.UI.Xaml.Window;

namespace Bible.Alarm.Platforms.Windows.Services.UI;

/// <summary>
/// Custom in-app toast for Windows. Uses a WinUI Popup overlay.
/// When a new toast arrives while one is showing, the old toast is dismissed immediately.
/// </summary>
public sealed partial class WindowsToastService(TaskScheduler taskScheduler, ILogger logger) : ToastService, IDisposable
{
    private bool isDisposed;
    private static readonly SemaphoreSlim @lock = new(1);

    private static CancellationTokenSource? activeCts;
    private static Popup? currentPopup;
    private static Window? currentWindow;

    public override async Task ShowMessage(string message, int seconds)
    {
        CancelActiveCts();

        if (!MainThread.IsMainThread)
        {
            await Task.Delay(0)
                .ContinueWith(async _ =>
                    await ShowAlert(message, seconds), taskScheduler);
        }
        else
        {
            await ShowAlert(message, seconds);
        }
    }

    private static async Task ShowAlert(string message, double seconds)
    {
        var cts = new CancellationTokenSource();

        await @lock.WaitAsync();
        try
        {
            activeCts = cts;

            try
            {
                await ToastLifecycleManager.CloseExistingPopupIfNeededAsync(currentPopup);
                var window = ToastWindowManager.GetNativeWindow();
                if (window is null)
                {
                    return;
                }

                var popup = ToastPopupFactory.CreateToastPopup(message, window);
                currentPopup = popup;
                await ShowFlyoutAsync(popup, window, seconds, cts.Token);
            }
            catch (COMException ex)
            {
                Log.Warning(ex, "COM exception occurred while showing toast message");
            }
            finally
            {
                if (currentPopup != null)
                {
                    ToastLifecycleManager.CleanupPopup(currentPopup);
                    currentPopup = null;
                }
            }
        }
        finally
        {
            if (activeCts == cts)
            {
                activeCts = null;
            }

            @lock.Release();
        }
    }

    private static void CancelActiveCts()
    {
        var existing = activeCts;
        if (existing == null)
        {
            return;
        }

        activeCts = null;
        try
        {
            existing.Cancel();
            existing.Dispose();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private static async Task ShowFlyoutAsync(Popup popup, Window window, double seconds, CancellationToken ct)
    {
        FrameworkElement? windowContent = null;

        try
        {
            windowContent = ToastLifecycleManager.SetupPopupAndGetWindowContent(popup, window);
            currentWindow = window;

            ToastPositionManager.SetInitialPopupPosition(popup, windowContent);
            popup.IsOpen = true;

            await Task.Delay(100, CancellationToken.None);
            ToastPositionManager.UpdatePopupPosition(popup, window);

            ToastPositionManager.SubscribeToWindowSizeChanges(windowContent, popup, window);

            try
            {
                await Task.Delay((int)(seconds * 1000), ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
        catch (COMException ex)
        {
            Log.Warning(ex, "COM exception occurred while showing popup flyout");
        }
        finally
        {
            await ToastLifecycleManager.CleanupFlyoutResources(currentWindow, popup);
        }
    }

    public override Task Clear()
    {
        CancelActiveCts();

        if (currentPopup != null)
        {
            try
            {
                currentPopup.IsOpen = false;
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Exception occurred while closing popup in Clear()");
            }
        }

        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        base.Dispose();
    }
}
