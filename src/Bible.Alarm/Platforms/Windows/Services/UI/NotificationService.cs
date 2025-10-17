using Bible.Alarm.Services.Contracts;
using NLog;
using System;
using System.Threading.Tasks;
using Bible.Alarm.Services.Windows.Handlers;
using Microsoft.Windows.AppNotifications;
using Windows.UI.Notifications;
using System.Linq;
using Bible.Alarm.Services.Windows.Helpers;
using Bible.Alarm.Models;

namespace Bible.Alarm.Services.Windows
{
    public class UwpNotificationService(IContainer container) : INotificationService
    {
        public async Task ShowNotification(long scheduleId)
        {
            var uwpAlarmHandler = container.Resolve<UwpAlarmHandler>();
            await uwpAlarmHandler.Handle(scheduleId, true);
        }

        public Task ScheduleNotification(AlarmSchedule schedule,
            string title, string body)
        {

            var scheduleId = schedule.Id;
            var time = schedule.NextFireDate();
            // Construct the toast content using built-in Windows APIs
            var toastXml = global::Windows.UI.Notifications.ToastNotificationManager.GetTemplateContent(global::Windows.UI.Notifications.ToastTemplateType.ToastText02);
            var textNodes = toastXml.GetElementsByTagName("text");
            textNodes[0].AppendChild(toastXml.CreateTextNode(title));
            textNodes[1].AppendChild(toastXml.CreateTextNode(body));
            
            var audioElement = toastXml.CreateElement("audio");
            audioElement.SetAttribute("src", "ms-appx:///Resources/cool-alarm-tone-notification-sound.mp3");
            var toastElement = toastXml.SelectSingleNode("/toast");
            toastElement.AppendChild(audioElement);
            // Add launch arguments
            var launchAttribute = toastXml.CreateAttribute("launch");
            launchAttribute.Value = scheduleId.ToString();
            ((global::Windows.Data.Xml.Dom.XmlElement)toastElement).SetAttributeNode(launchAttribute);

            // Create the toast notification object.
            var toast = new ScheduledToastNotification(toastXml, time)
            {
                Id = scheduleId.ToString()
            };
           
            // Add to the schedule.
            ToastNotificationManager.CreateToastNotifier()
                .AddToSchedule(toast);

            return Task.CompletedTask;
        }

        public Task Remove(long scheduleId)
        {
            var notifier = ToastNotificationManager.CreateToastNotifier();

            // Get the list of scheduled toasts that haven't appeared yet
            var scheduledToasts = notifier.GetScheduledToastNotifications();

            // Find our scheduled toast we want to cancel
            var toRemove = scheduledToasts.FirstOrDefault(i => i.Id == scheduleId.ToString());
            if (toRemove != null)
            {
                // And remove it from the schedule
                notifier.RemoveFromSchedule(toRemove);
            }

            return Task.CompletedTask;
        }

        public Task<bool> IsScheduled(long scheduleId)
        {
            var notifier = ToastNotificationManager.CreateToastNotifier();

            // Get the list of scheduled toasts that haven't appeared yet
            var scheduledToasts = notifier.GetScheduledToastNotifications();

            // Find our scheduled toast we want to cancel
            var existing = scheduledToasts.FirstOrDefault(i => i.Id == scheduleId.ToString());
            if (existing != null)
            {
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }

        public Task<bool> CanSchedule()
        {
            return Task.FromResult(BootstrapHelper.IsBackgroundTaskEnabled);
        }

        public void Dispose()
        {

        }
    }

}
