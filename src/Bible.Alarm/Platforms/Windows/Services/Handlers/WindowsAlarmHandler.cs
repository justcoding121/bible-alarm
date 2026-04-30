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
                    logger.Information(AppConstants.Logging.ProcessDiagnosticsLog.AlarmTriggeredWhilePlaybackActiveStoppingForNewAlarm, scheduleId);
                    try
                    {
                        await playbackService.StopAsync();
                    }
                    catch (Exception e)
                    {
                        logger.Warning(e, AppConstants.Logging.ProcessDiagnosticsLog.ErrorStoppingPlaybackBeforeAlarmForSchedule, scheduleId);
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
                        throw new InvalidOperationException(AppConstants.Logging.AlarmDiagnostics.RingingAlarmFailed, e);
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
