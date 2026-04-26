using Bible.Alarm.Common.Interfaces.Media;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Platforms.Android.Services.UI;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores;
using CommunityToolkit.Mvvm.Messaging;
using Fluxor;
using Serilog;

namespace Bible.Alarm.Platforms.Android.Services.Handlers;

public sealed class AndroidAlarmHandler(
    ILogger logger,
    IPlaybackService playbackService,
    IAlarmScheduleService alarmScheduleService,
    IState<PlaybackState> playbackState)
    : IAndroidAlarmHandler
{
    public async Task HandleAsync(int scheduleId, bool isAlarm)
    {
        await TryStopPlaybackForOverlappingAlarmAsync(scheduleId, isAlarm);

        var schedule = await alarmScheduleService.GetScheduleByIdAsync(
            scheduleId, false, false);

        if (schedule == null)
        {
            logger.Warning("Schedule {ScheduleId} not found - stopping foreground service", scheduleId);
            Platforms.Android.Services.Media.ForegroundServiceCoordinator.StopAlarmForegroundServiceIfActive();
            return;
        }

        if (isAlarm && schedule.NotificationEnabled)
        {
            PresentTapNotificationAndReturn(schedule, scheduleId);
            return;
        }

        if (isAlarm && !schedule.NotificationEnabled)
        {
            logger.Information("Alarm triggered for schedule {ScheduleId} with tap disabled - using foreground service notification (no sound)", scheduleId);
        }

        if (!isAlarm)
        {
            await StartUserInitiatedPlaybackAsync(schedule);
            return;
        }

        await StartAlarmPlaybackAsync(scheduleId);
    }

    private async Task TryStopPlaybackForOverlappingAlarmAsync(int scheduleId, bool isAlarm)
    {
        if (!isAlarm || !playbackState.Value.IsPreparingOrPlaying)
        {
            return;
        }

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

    private void PresentTapNotificationAndReturn(AlarmSchedule schedule, int scheduleId)
    {
        logger.Information("Alarm triggered with NotificationEnabled=true for schedule {ScheduleId} - stopping foreground service and showing tap notification", scheduleId);
        Platforms.Android.Services.Media.ForegroundServiceCoordinator.StopAlarmForegroundServiceIfActive();

        logger.Debug("Removing any existing local notification for schedule {ScheduleId}", schedule.Id);
        AndroidNotificationService.RemoveLocalNotification(schedule.Id);

        var notificationTitle = string.IsNullOrEmpty(schedule.Name) ? "Bible Alarm" : schedule.Name;
        logger.Information("Showing local notification for schedule {ScheduleId} - Title={Title}", schedule.Id, notificationTitle);
        AndroidNotificationService.ShowLocalNotification(schedule.Id,
            notificationTitle,
            "Press to start listening now.");

        logger.Information("Local notification shown for schedule {ScheduleId} - waiting for user tap", schedule.Id);
    }

    private async Task StartUserInitiatedPlaybackAsync(AlarmSchedule schedule)
    {
        AndroidNotificationService.RemoveLocalNotification(schedule.Id);
        logger.Information("User-initiated playback for schedule {ScheduleId} - starting playback directly without notifications", schedule.Id);

        await StartPrepareAndPlayInBackgroundAsync(schedule.Id, isAlarm: false, errorLog: "An error happened when starting user-initiated playback.");
    }

    private async Task StartAlarmPlaybackAsync(int scheduleId)
    {
        await StartPrepareAndPlayInBackgroundAsync(scheduleId, isAlarm: true, errorLog: "An error happened when ringing the alarm.");
    }

    private async Task StartPrepareAndPlayInBackgroundAsync(int scheduleId, bool isAlarm, string errorLog)
    {
        WeakReferenceMessenger.Default.Send(new RequestShowPlaybackModalMessage { TargetScheduleId = scheduleId });
        await Task.Run(async () =>
        {
            try
            {
                await playbackService.PrepareAndPlayAsync(scheduleId, isAlarm);
            }
            catch (Exception e)
            {
                logger.Error(e, errorLog);
                Platforms.Android.Services.Media.ForegroundServiceCoordinator.StopAlarmForegroundServiceIfActive();
            }
        });
    }
}
