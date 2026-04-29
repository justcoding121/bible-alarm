using Bible.Alarm.Shared.Constants;
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
    private static readonly SemaphoreSlim PlayLock = new(1, 1);

    private static readonly TimeSpan PlayLockOverallTimeout = TimeSpan.FromSeconds(25);

    public async Task PlayScheduleAsync(int scheduleId)
    {
        if (scheduleId <= 0)
        {
            return;
        }

        // Non-blocking acquire: if another play is already in progress, return immediately
        // instead of queuing behind it. Queuing causes a cascade where rapid taps hold
        // PlayLock for minutes (each play attempt can take 10-30s).
        if (!await PlayLock.WaitAsync(0))
        {
            logger.Warning("PlayScheduleAsync: PlayLock already held — skipping (schedule {ScheduleId})", scheduleId);
            return;
        }

        try
        {
            // Overall timeout prevents PlayLock from being held indefinitely if
            // PrepareAndPlayAsync hangs (e.g. native player stuck, network call hung).
            // After timeout, PlayLock is released so the next play attempt can proceed.
            var playTask = PlayScheduleCoreAsync(scheduleId);
            var completedTask = await Task.WhenAny(playTask, Task.Delay(PlayLockOverallTimeout));

            if (completedTask != playTask)
            {
                logger.Error("PlayScheduleAsync: overall timeout ({Timeout}s) for schedule {ScheduleId}. Releasing PlayLock — background task continues.",
                    PlayLockOverallTimeout.TotalSeconds, scheduleId);
                return;
            }

            await playTask;
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
        catch (OperationCanceledException)
        {
            logger.Debug("Playback cancelled for schedule {ScheduleId}", scheduleId);
        }
        catch (Exception e)
        {
            logger.Information(e, AppConstants.Logging.AlarmDiagnostics.PlayingAlarmFailed);
            dispatcher.Dispatch(new PlaybackStatusChangedAction(PlayStatus.Failed));
            await toastService.ShowMessage("Network may not be available, please try again", 5);
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
        await toastService.ShowMessage("Cannot update the track when schedule is in progress");

        return false;
    }

}

