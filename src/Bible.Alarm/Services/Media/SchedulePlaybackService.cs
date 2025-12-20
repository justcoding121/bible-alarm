using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Stores;
using Fluxor;
using Serilog;

namespace Bible.Alarm.Services.Media;

public class SchedulePlaybackService(
    ILogger logger,
    IServiceScopeFactory scopeFactory,
    IState<PlaybackState> playbackState)
    : ISchedulePlaybackService, IDisposable
{
    private bool isDisposed;

    public async Task PlayScheduleAsync(int scheduleId)
    {
        if (scheduleId <= 0)
        {
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var toastService = scope.ServiceProvider.GetRequiredService<IToastService>();
        var playbackService = scope.ServiceProvider.GetRequiredService<IPlaybackService>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

        try
        {
            await playbackService.PrepareAndPlayAsync(scheduleId, false);
            // Note: ShowNotificationAsync is not called here because it triggers AlarmRingerReceiver
            // which would cause duplicate playback. Notifications are only shown when alarms actually fire.
        }
        catch (Exception e)
        {
            logger.Information(e, "An error happened when playing alarm.");
            await toastService.ShowMessage("Error. Network may not be available. Please try again.", 5);
        }
    }

    public async Task<bool> CanMoveChapterAsync(int scheduleId)
    {
        using var scope = scopeFactory.CreateScope();

        if (!playbackState.Value.IsPreparingOrPlaying || playbackState.Value.CurrentScheduleId != scheduleId)
        {
            return true;
        }

        var toastService = scope.ServiceProvider.GetRequiredService<IToastService>();
        await toastService.ShowMessage("Cannot update the chapter when schedule is in progress.");

        return false;
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;

        // All injected services are singletons, so don't dispose them
        // No event handlers to unsubscribe
    }
}

