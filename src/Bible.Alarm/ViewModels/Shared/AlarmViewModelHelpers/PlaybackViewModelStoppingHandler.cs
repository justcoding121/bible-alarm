namespace Bible.Alarm.ViewModels.Shared.AlarmViewModelHelpers;

/// <summary>
/// Handles stopping and progress reset UI for PlaybackViewModel.
/// </summary>
public static class PlaybackViewModelStoppingHandler
{
    public static void BeginStoppingUi(Action apply)
    {
        if (MainThread.IsMainThread)
        {
            apply();
            return;
        }
        MainThread.BeginInvokeOnMainThread(apply);
    }

    public static void ResetProgressUi(Func<bool> isUserInteracting, Action apply)
    {
        if (isUserInteracting())
        {
            return;
        }
        if (MainThread.IsMainThread)
        {
            apply();
            return;
        }
        MainThread.BeginInvokeOnMainThread(apply);
    }
}
