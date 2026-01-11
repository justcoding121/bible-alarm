#nullable enable

using System.Runtime.InteropServices;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Platforms.Windows.Services.UI.WindowsToastServiceHelpers;
using Bible.Alarm.Services.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Serilog;
using Window = Microsoft.UI.Xaml.Window;

namespace Bible.Alarm.Platforms.Windows.Services.UI;

public sealed partial class WindowsToastService(TaskScheduler taskScheduler, ILogger logger) : ToastService, IDisposable
{
    private bool isDisposed;
    private static readonly SemaphoreSlim @lock = new(1);

    private static TaskCompletionSource<bool>? clearRequest;
    private static Popup? currentPopup;
    private static Window? currentWindow;

    public override Task Clear()
    {
        if (clearRequest is { } request)
        {
            request.SetResult(true);
        }

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

    public override async Task ShowMessage(string message, int seconds)
    {
        if (clearRequest is not null)
        {
            return;
        }

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
        clearRequest = new TaskCompletionSource<bool>();

        await ConcurrencyHelper.ExecuteAsync(@lock, async () =>
        {
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
                await ShowFlyoutAsync(popup, window, seconds);
            }
            catch (COMException ex)
            {
                Log.Warning(ex, "COM exception occurred while showing toast message. This can happen when manipulating UI elements from wrong thread or during cleanup");
            }
            finally
            {
                if (currentPopup != null)
                {
                    ToastLifecycleManager.CleanupPopup(currentPopup);
                    currentPopup = null;
                }
            }
        });

        clearRequest = null;
    }

    private static async Task ShowFlyoutAsync(Popup popup, Window window, double seconds)
    {
        FrameworkElement? windowContent = null;

        try
        {
            windowContent = ToastLifecycleManager.SetupPopupAndGetWindowContent(popup, window);
            currentWindow = window;

            ToastPositionManager.SetInitialPopupPosition(popup, windowContent);
            popup.IsOpen = true;

            await Task.Delay(100);
            ToastPositionManager.UpdatePopupPosition(popup, window);

            ToastPositionManager.SubscribeToWindowSizeChanges(windowContent, popup, window);
            await WaitForDisplayDuration(seconds);
        }
        catch (COMException ex)
        {
            Log.Warning(ex, "COM exception occurred while showing popup flyout. Closing popup and continuing");
        }
        finally
        {
            await ToastLifecycleManager.CleanupFlyoutResources(currentWindow, popup);
        }
    }

    private static async Task WaitForDisplayDuration(double seconds)
    {
        if (clearRequest is { } request)
        {
            await Task.WhenAny(request.Task, Task.Delay((int)(seconds * 1000)));
        }
        else
        {
            await Task.Delay((int)(seconds * 1000));
        }
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
