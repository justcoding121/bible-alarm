using Bible.Alarm.iOS.Extensions;
using Bible.Alarm.iOS.Services.Handlers;
using Bible.Alarm.Models;
using Bible.Alarm.Services.Contracts;
using NLog;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UserNotifications;

namespace Bible.Alarm.Services.iOS
{
    public class iOSNotificationService : INotificationService
    {
        private static readonly Lazy<Logger> lazyLogger = new Lazy<Logger>(() => LogManager.GetCurrentClassLogger());
        private static Logger logger => lazyLogger.Value;


        private readonly IContainer container;
        private readonly TaskScheduler taskScheduler;

        public iOSNotificationService(IContainer container)
        {
            this.container = container;
            taskScheduler = container.Resolve<TaskScheduler>();
        }

        public async Task ShowNotification(long scheduleId)
        {
            var iosAlarmHandler = container.Resolve<iOSAlarmHandler>();
            await iosAlarmHandler.Handle(scheduleId, true);
        }

        public async Task ScheduleNotification(AlarmSchedule schedule,
        string title, string body)
        {

            var scheduleId = schedule.Id;
            var time = schedule.NextFireDate();
            var daysOfWeek = schedule.DaysOfWeek;

            await Task.Delay(0).
                ContinueWith((x) =>
                {
                    var @params = new Dictionary<string, string>
                    {
                        {"ScheduleId", scheduleId.ToString()},
                    };

                    var content = new UNMutableNotificationContent();
                    content.Title = title;
                    content.Body = body;
                    content.Sound = UNNotificationSound.GetSound("cool-alarm-tone-notification-sound.mp3");
                    content.UserInfo = @params.ToNSDictionary();
                    content.Badge = 1;

                    foreach (var day in daysOfWeek.ToWeekDays())
                    {
                        var trigger = UNCalendarNotificationTrigger.CreateTrigger(time.LocalDateTime.ToNSDateComponents(day), true);

                        var requestId = $"{scheduleId}_{day}";
                        var request = UNNotificationRequest.FromIdentifier(requestId, content, trigger);

                        UNUserNotificationCenter.Current.AddNotificationRequest(request, (err) =>
                        {
                            if (err != null)
                            {
                                logger.Error($"An error happened when scheduling ios notification. code: {err.Code}");
                            }
                        });
                    }

                }, taskScheduler);

        }

        public async Task Remove(long scheduleId)
        {
            await Task.Delay(0).
               ContinueWith((x) =>
               {
                   var pending = UNUserNotificationCenter.Current.GetPendingNotificationRequestsAsync().Result;

                   if (pending != null)
                   {
                       foreach (var notification in pending)
                       {
                           if (notification.Identifier.StartsWith($"{scheduleId}_")
                                || notification.Identifier == scheduleId.ToString())
                           {
                               UNUserNotificationCenter.Current.RemovePendingNotificationRequests(new[] { notification.Identifier });
                           }

                       }
                   }

               }, taskScheduler);
        }

        public async Task<bool> IsScheduled(long scheduleId)
        {
            return await Task.Delay(0).
                 ContinueWith((x) =>
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

                 }, taskScheduler);
        }

        public async Task<bool> CanSchedule()
        {
            return await Task.Delay(0).
               ContinueWith((x) =>
               {
                   var taskCompletionSource = new TaskCompletionSource<bool>();

                   UNUserNotificationCenter.Current.GetNotificationSettings((settings) =>
                    {
                        var result = settings.AlertSetting == UNNotificationSetting.Enabled;
                        taskCompletionSource.SetResult(result);
                    });

                   return taskCompletionSource.Task.Result;

               }, taskScheduler);

        }

        public void Dispose()
        {

        }
    }

}
