using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Platforms.iOS.Services.Handlers.Interfaces;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using CommunityToolkit.Mvvm.Messaging;
using Fluxor;
using Serilog;
using UIKit;

namespace Bible.Alarm.Platforms.iOS.Services.Handlers;

public sealed class IOsAlarmHandler(
    ILogger logger,
    IPlaybackService playbackService,
    IState<PlaybackState> playbackState,
    TaskScheduler taskScheduler)
    : IIosAlarmHandler
{
    private static readonly SemaphoreSlim @lock = new(1);

    private static bool firstTime = true;

    public async Task HandleAsync(int scheduleId, bool isAlarm)
    {
        try
        {
            await ConcurrencyHelper.ExecuteAsync(@lock, async () =>
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

                await Task.Delay(0).ContinueWith(_ =>
                {
                    if (!firstTime)
                    {
                        UIApplication.SharedApplication.BeginReceivingRemoteControlEvents();
                    }

                    firstTime = false;
                }, taskScheduler);


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
