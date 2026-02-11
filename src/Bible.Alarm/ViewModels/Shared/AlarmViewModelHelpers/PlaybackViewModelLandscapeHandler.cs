#nullable enable

namespace Bible.Alarm.ViewModels.Shared.AlarmViewModelHelpers;

/// <summary>
/// Handles landscape overlay controls visibility and auto-hide for PlaybackViewModel.
/// </summary>
public sealed class PlaybackViewModelLandscapeHandler
{
    private const int AutoHideMs = 3000;

    private CancellationTokenSource? autoHideCts;

    public void CancelAutoHide()
    {
        try
        {
            autoHideCts?.Cancel();
            autoHideCts?.Dispose();
        }
        catch
        {
            // ignore
        }
        finally
        {
            autoHideCts = null;
        }
    }

    public void ScheduleAutoHide(
        Func<bool> isLandscape,
        Func<bool> shouldCancel,
        Action setOverlayVisibleFalse)
    {
        CancelAutoHide();
        if (!isLandscape())
        {
            return;
        }

        var cts = new CancellationTokenSource();
        autoHideCts = cts;
        var token = cts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(AutoHideMs, token);
            }
            catch
            {
                return;
            }

            if (token.IsCancellationRequested)
            {
                return;
            }

            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (!isLandscape() || shouldCancel())
                {
                    return;
                }
                setOverlayVisibleFalse();
            });
        }, token);
    }
}
