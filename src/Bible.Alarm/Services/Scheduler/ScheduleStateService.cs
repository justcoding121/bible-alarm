using Bible.Alarm.Common;
using Bible.Alarm.Common.Interfaces.Scheduler;
using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Database;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Stores.Actions.Schedule;
using Fluxor;
using Microsoft.EntityFrameworkCore;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Services.Scheduler;

public class ScheduleStateService(
    ILogger logger,
    IServiceScopeFactory scopeFactory,
    IAlarmService alarmService,
    INotificationService notificationService,
    IToastService toastService,
    IDispatcher dispatcher)
    : IScheduleStateService
{
    private readonly ILogger _logger = logger;
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly IAlarmService _alarmService = alarmService;
    private readonly INotificationService _notificationService = notificationService;
    private readonly IToastService _toastService = toastService;
    private readonly IDispatcher _dispatcher = dispatcher;

    public async Task<bool> UpdateScheduleEnabledStateAsync(int scheduleId, bool isEnabled)
    {
        // Check notification permissions if enabling
        if (isEnabled &&
            (DeviceInfo.Platform == DevicePlatform.iOS
             || DeviceInfo.Platform == DevicePlatform.WinUI)
            && !await _notificationService.CanSchedule())
        {
            if (DeviceInfo.Platform == DevicePlatform.iOS)
                await _toastService.ShowMessage(
                    "Cannot schedule alarm because you've disabled notifications. " +
                    "Please enable notification for this app under system settings.", 7);
            else
                await _toastService.ShowMessage(
                    "Cannot schedule alarm because you've denied background apps permission. " +
                    "Please grant background apps permission for this app under system settings.", 7);

            return false; // Indicates the state change was rejected
        }

        // Update database and alarm service
        AlarmSchedule updatedSchedule = null;
        await Task.Run(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            await using var scheduleDbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();
            var existing = await scheduleDbContext.AlarmSchedules
                .Include(x => x.BibleReadingSchedule)
                .Include(x => x.Music)
                .FirstAsync(x => x.Id == scheduleId);
            existing.IsEnabled = isEnabled;
            await scheduleDbContext.SaveChangesAsync();

            _alarmService.Update(existing);
            updatedSchedule = existing;
        });

        // Update the Fluxor store to trigger state change and UI refresh
        if (updatedSchedule != null)
        {
            _dispatcher.Dispatch(new UpdateScheduleAction(updatedSchedule));
        }

        // Show notification if enabled
        if (isEnabled && updatedSchedule != null)
        {
            await _toastService.ShowScheduledNotification(updatedSchedule);
        }

        return true; // Indicates the state change was successful
    }
}

