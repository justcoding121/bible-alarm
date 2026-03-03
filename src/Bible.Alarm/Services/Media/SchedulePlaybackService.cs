using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Playback;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Services.Media;

public sealed class SchedulePlaybackService(
    ILogger logger,
    IServiceScopeFactory scopeFactory,
    IState<PlaybackState> playbackState,
    IDispatcher dispatcher)
    : ISchedulePlaybackService
{
    /// <summary>
    /// Ensures only one play request runs at a time. A second tap waits for the first to finish
    /// (success or fail) then runs, avoiding concurrent preparation and stuck IsBusy state.
    /// </summary>
    private static readonly SemaphoreSlim PlayLock = new(1, 1);

    public async Task PlayScheduleAsync(int scheduleId)
    {
        if (scheduleId <= 0)
        {
            return;
        }

        await PlayLock.WaitAsync();
        try
        {
            await PlayScheduleCoreAsync(scheduleId);
        }
        finally
        {
            PlayLock.Release();
        }
    }

    private async Task PlayScheduleCoreAsync(int scheduleId)
    {
        using var scope = scopeFactory.CreateScope();
        var toastService = scope.ServiceProvider.GetRequiredService<IToastService>();
        var playbackService = scope.ServiceProvider.GetRequiredService<IPlaybackService>();

        try
        {
            // Remove any existing alarm notifications before starting user-initiated playback
            // This ensures no notification sounds when user clicks play from home page
#if ANDROID
            Platforms.Android.Services.UI.AndroidNotificationService.RemoveLocalNotification(scheduleId);
            // Also stop any alarm foreground service that might be active
            Platforms.Android.Services.Media.ForegroundServiceCoordinator.StopAlarmForegroundServiceIfActive();
#endif

            await playbackService.PrepareAndPlayAsync(scheduleId, false);
            // Note: ShowNotificationAsync is not called here because it triggers AlarmRingerReceiver
            // which would cause duplicate playback. Notifications are only shown when alarms actually fire.
        }
        catch (Exception e)
        {
            logger.Information(e, "An error happened when playing alarm.");
            dispatcher.Dispatch(new PlaybackStatusChangedAction(PlayStatus.Failed));
            await toastService.ShowMessage("Error. Network may not be available. Please try again.", 5);
        }
    }

    public async Task<bool> CanMoveTrackAsync(int scheduleId)
    {
        using var scope = scopeFactory.CreateScope();

        if (!playbackState.Value.IsPreparingOrPlaying || playbackState.Value.CurrentScheduleId != scheduleId)
        {
            return true;
        }

        var toastService = scope.ServiceProvider.GetRequiredService<IToastService>();
        await toastService.ShowMessage("Cannot update the track when schedule is in progress.");

        return false;
    }

}

