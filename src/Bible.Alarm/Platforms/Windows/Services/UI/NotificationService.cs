using Bible.Alarm.Services.Contracts;
using NLog;
using System;
using System.Threading.Tasks;
using Bible.Alarm.UWP.Services.Handlers;
using Microsoft.Toolkit.Uwp.Notifications;
using Windows.UI.Notifications;
using System.Linq;
using Bible.Alarm.Services.Uwp.Helpers;
using Bible.Alarm.Models;

namespace Bible.Alarm.Services.UWP
{
    public class UwpNotificationService : INotificationService
    {
        private readonly IContainer container;

        public UwpNotificationService(IContainer container)
        {
            this.container = container;
        }

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
            // Construct the toast content
            ToastContent toastContent = new ToastContent()
            {
                Visual = new ToastVisual()
                {
                    BindingGeneric = new ToastBindingGeneric()
                    {
                        Children =
                        {
                            new AdaptiveText()
                            {
                                Text = title
                            },

                            new AdaptiveText()
                            {
                                Text = body
                            }
                        }
                    }
                },
                Audio = new ToastAudio()
                {
                    Src = new Uri("ms-appx:///Resources/cool-alarm-tone-notification-sound.mp3")
                },
                // Arguments when the user taps body of toast
                Launch = scheduleId.ToString()
            };

            // Create the toast notification object.
            var toast = new ScheduledToastNotification(toastContent.GetXml(), time)
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
