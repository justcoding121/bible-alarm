using System.Linq;
using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Platforms.iOS.Extensions;
using Bible.Alarm.Platforms.iOS.Services.Handlers.Interfaces;
using Bible.Alarm.Shared.Models.Schedule;
using Serilog;
using UserNotifications;

namespace Bible.Alarm.Platforms.iOS.Services.UI;

public sealed class IOsNotificationService(ILogger logger, IServiceScopeFactory scopeFactory) : INotificationService
{
    public async Task ShowNotificationAsync(int scheduleId)
    {
        using var scope = scopeFactory.CreateScope();
        var iosAlarmHandler = scope.ServiceProvider.GetRequiredService<IIosAlarmHandler>();
        await iosAlarmHandler.HandleAsync(scheduleId, true);
    }

    public async Task ScheduleNotificationAsync(AlarmSchedule alarmSchedule,
        string title, string body)
    {
        var scheduleId = alarmSchedule.Id;
        var time = alarmSchedule.NextFireDate();
        var daysOfWeek = alarmSchedule.DaysOfWeek;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            var @params = new Dictionary<string, string>
            {
                { "ScheduleId", scheduleId.ToString() }
            };

            var content = new UNMutableNotificationContent();
            content.Title = title;
            content.Body = body;
            content.Sound = UNNotificationSound.Default;
            content.UserInfo = @params.ToNsDictionary();
            content.Badge = 1;
            content.InterruptionLevel = UNNotificationInterruptionLevel.TimeSensitive2;

            foreach (var day in daysOfWeek.ToWeekDays())
            {
                var trigger =
                    UNCalendarNotificationTrigger.CreateTrigger(time.LocalDateTime.ToNsDateComponents(day), true);

                var requestId = $"{scheduleId}_{day}";
                var request = UNNotificationRequest.FromIdentifier(requestId, content, trigger);

                UNUserNotificationCenter.Current.AddNotificationRequest(request, err =>
                {
                    if (err != null)
                    {
                        logger.Error($"An error happened when scheduling ios notification. code: {err.Code}");
                    }
                });
            }
        });
    }

    public async Task ClearDeliveredNotificationAsync(int scheduleId)
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var delivered = await UNUserNotificationCenter.Current.GetDeliveredNotificationsAsync();
            if (delivered == null)
            {
                return;
            }

            var toRemove = delivered
                .Where(n => n.Request.Identifier.StartsWith($"{scheduleId}_", StringComparison.Ordinal)
                    || n.Request.Identifier == scheduleId.ToString())
                .Select(n => n.Request.Identifier)
                .ToList();

            if (toRemove.Count > 0)
            {
                UNUserNotificationCenter.Current.RemoveDeliveredNotifications(toRemove.ToArray());
            }
        });
    }

    public async Task RemoveAsync(int scheduleId)
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            var pending = UNUserNotificationCenter.Current.GetPendingNotificationRequestsAsync().Result;

            if (pending != null)
            {
                var prefix = $"{scheduleId}_";
                var idString = scheduleId.ToString();
                foreach (var notification in pending.Where(n =>
                             n.Identifier.StartsWith(prefix, StringComparison.Ordinal)
                             || n.Identifier == idString))
                {
                    UNUserNotificationCenter.Current.RemovePendingNotificationRequests([notification.Identifier
                    ]);
                }
            }
        });
    }

    public async Task<bool> IsScheduledAsync(int scheduleId)
    {
        return await MainThread.InvokeOnMainThreadAsync(() =>
        {
            var pending = UNUserNotificationCenter.Current.GetPendingNotificationRequestsAsync().Result;

            if (pending != null)
            {
                var prefix = $"{scheduleId}_";
                var idString = scheduleId.ToString();
                if (pending.Any(n =>
                        n.Identifier.StartsWith(prefix, StringComparison.Ordinal)
                        || n.Identifier == idString))
                {
                    return true;
                }
            }

            return false;
        });
    }

    public async Task<bool> CanScheduleAsync()
    {
        return await MainThread.InvokeOnMainThreadAsync(() =>
        {
            var taskCompletionSource = new TaskCompletionSource<bool>();

            UNUserNotificationCenter.Current.GetNotificationSettings(settings =>
            {
                var result = settings.AlertSetting == UNNotificationSetting.Enabled;
                taskCompletionSource.SetResult(result);
            });

            return taskCompletionSource.Task.Result;
        });
    }
}
