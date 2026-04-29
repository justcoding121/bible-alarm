using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Platforms.Windows.Services.Handlers.Interfaces;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using CommunityToolkit.Mvvm.Messaging;
using Fluxor;
using Serilog;

namespace Bible.Alarm.Platforms.Windows.Services.Handlers;

public sealed class WindowsAlarmHandler(
    ILogger logger,
    IPlaybackService playbackService,
    IState<PlaybackState> playbackState) : IWindowsAlarmHandler
{
    private static readonly SemaphoreSlim @lock = new(1);

    public async Task HandleAsync(int scheduleId, bool isAlarm)
    {
        try
        {
            await Common.Helpers.ConcurrencyHelper.ExecuteAsync(@lock, async () =>
            {
                if (playbackState.Value.IsPreparingOrPlaying)
                {
                    logger.Information("Alarm triggered for schedule {ScheduleId} while playback is active - stopping current playback to handle new alarm", scheduleId);
                    try
                    {
                        await playbackService.StopAsync();
                    }
                    catch (Exception e)
                    {
                        logger.Warning(e, "Error stopping current playback before handling alarm for schedule {ScheduleId}", scheduleId);
                    }
                }

                WeakReferenceMessenger.Default.Send(new RequestShowPlaybackModalMessage { TargetScheduleId = scheduleId });
                await Task.Run(async () =>
                {
                    try
                    {
                        await playbackService.PrepareAndPlayAsync(scheduleId, isAlarm);
                    }
                    catch (Exception e)
                    {
                        logger.Error(e, AppConstants.Logging.AlarmDiagnostics.RingingAlarmFailed);
                        throw;
                    }
                });
            });
        }
        catch (Exception e)
        {
            logger.Error(e, AppConstants.Logging.AlarmDiagnostics.CreatingAlarmRingTaskFailed);
        }
    }
}
