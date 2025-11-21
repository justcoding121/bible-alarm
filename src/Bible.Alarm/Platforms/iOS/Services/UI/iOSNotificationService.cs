using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Platforms.iOS.Extensions;
using Bible.Alarm.Platforms.iOS.Services.Handlers;
using Serilog;
using UserNotifications;

namespace Bible.Alarm.Platforms.iOS.Services.UI
{
    public class iOSNotificationService(ILogger logger, IServiceScopeFactory scopeFactory) : INotificationService
    {
        private readonly ILogger _logger = logger;
        private readonly IServiceScopeFactory _scopeFactory = scopeFactory;

        public async Task ShowNotificationAsync(int scheduleId)
        {
            using var scope = _scopeFactory.CreateScope();
            var iosAlarmHandler = scope.ServiceProvider.GetRequiredService<iOSAlarmHandler>();
            await iosAlarmHandler.HandleAsync(scheduleId, true);
        }

        public async Task ScheduleNotificationAsync(AlarmSchedule schedule,
            string title, string body)
        {
            var scheduleId = schedule.Id;
            var time = schedule.NextFireDate();
            var daysOfWeek = schedule.DaysOfWeek;

            await Task.Delay(0).ContinueWith(_ =>
            {
                var @params = new Dictionary<string, string>
                {
                    { "ScheduleId", scheduleId.ToString() }
                };

                var content = new UNMutableNotificationContent();
                content.Title = title;
                content.Body = body;
                content.Sound = UNNotificationSound.GetSound("cool-alarm-tone-notification-sound.mp3");
                content.UserInfo = @params.ToNsDictionary();
                content.Badge = 1;

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
                            _logger.Error($"An error happened when scheduling ios notification. code: {err.Code}");
                        }
                    });
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        public async Task RemoveAsync(int scheduleId)
        {
            await Task.Delay(0).ContinueWith(_ =>
            {
                var pending = UNUserNotificationCenter.Current.GetPendingNotificationRequestsAsync().Result;

                if (pending != null)
                {
                    foreach (var notification in pending)
                    {
                        if (notification.Identifier.StartsWith($"{scheduleId}_")
                            || notification.Identifier == scheduleId.ToString())
                        {
                            UNUserNotificationCenter.Current.RemovePendingNotificationRequests([notification.Identifier
                            ]);
                        }
                    }
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        public async Task<bool> IsScheduledAsync(int scheduleId)
        {
            return await Task.Delay(0).ContinueWith(_ =>
            {
                var pending = UNUserNotificationCenter.Current.GetPendingNotificationRequestsAsync().Result;

                if (pending != null)
                {
                    foreach (var notification in pending)
                    {
                        if (notification.Identifier.StartsWith($"{scheduleId}_")
                            || notification.Identifier == scheduleId.ToString())
                        {
                            return true;
                        }
                    }
                }

                return false;
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        public async Task<bool> CanScheduleAsync()
        {
            return await Task.Delay(0).ContinueWith(_ =>
            {
                var taskCompletionSource = new TaskCompletionSource<bool>();

                UNUserNotificationCenter.Current.GetNotificationSettings(settings =>
                {
                    var result = settings.AlertSetting == UNNotificationSetting.Enabled;
                    taskCompletionSource.SetResult(result);
                });

                return taskCompletionSource.Task.Result;
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        public void Dispose()
        {
        }
    }
}