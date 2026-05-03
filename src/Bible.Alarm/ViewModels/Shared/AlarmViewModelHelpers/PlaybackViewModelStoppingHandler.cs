using Bible.Alarm.Services.UI.Interfaces;

namespace Bible.Alarm.ViewModels.Shared.AlarmViewModelHelpers;

/// <summary>
/// Handles stopping and progress reset UI for PlaybackViewModel.
/// </summary>
public static class PlaybackViewModelStoppingHandler
{
    public static void BeginStoppingUi(IMainThreadScheduler mainThread, Action apply)
    {
        if (mainThread.IsMainThread)
        {
            apply();
            return;
        }
        mainThread.BeginInvokeOnMainThread(apply);
    }

    public static void ResetProgressUi(IMainThreadScheduler mainThread, Func<bool> isUserInteracting, Action apply)
    {
        if (isUserInteracting())
        {
            return;
        }
        if (mainThread.IsMainThread)
        {
            apply();
            return;
        }
        mainThread.BeginInvokeOnMainThread(apply);
    }
}
