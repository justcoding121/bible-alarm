using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Platforms.Windows.Helpers;
using Bible.Alarm.Platforms.Windows.Services.Handlers;
// Removed UWP toast notification APIs - using WinUI 3 alternatives

namespace Bible.Alarm.Platforms.Windows.Services.UI
{
    public class WindowsNotificationService(WindowsAlarmHandler windowsAlarmHandler) : INotificationService
    {
        private readonly WindowsAlarmHandler _windowsAlarmHandler = windowsAlarmHandler;

        public async Task ShowNotification(int scheduleId)
        {
            await _windowsAlarmHandler.Handle(scheduleId, true);
        }

        public Task ScheduleNotification(AlarmSchedule schedule,
            string title, string body)
        {
            // For WinUI 3 desktop apps, we can't use UWP toast notifications
            // This functionality would need to be implemented using alternative approaches
            // such as Windows Task Scheduler, Windows Notifications API, or a custom solution
            // For now, we'll return a completed task without scheduling
            // TODO: Implement proper notification scheduling for WinUI 3 desktop apps
            return Task.CompletedTask;
        }

        public Task Remove(int scheduleId)
        {
            // For WinUI 3 desktop apps, we can't use UWP toast notifications
            // This functionality would need to be implemented using alternative approaches
            // For now, we'll return a completed task without removing
            // TODO: Implement proper notification removal for WinUI 3 desktop apps
            return Task.CompletedTask;
        }

        public Task<bool> IsScheduled(int scheduleId)
        {
            // For WinUI 3 desktop apps, we can't use UWP toast notifications
            // This functionality would need to be implemented using alternative approaches
            // For now, we'll return false
            // TODO: Implement proper notification checking for WinUI 3 desktop apps
            return Task.FromResult(false);
        }

        public Task<bool> CanSchedule()
        {
            return Task.FromResult(WindowsBootstrapHelper.IsBackgroundTaskEnabled);
        }

        public void Dispose()
        {
        }
    }
}