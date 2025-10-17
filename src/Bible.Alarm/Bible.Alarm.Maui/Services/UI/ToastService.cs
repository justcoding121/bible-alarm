using Bible.Alarm.Models;
using Bible.Alarm.Services.Contracts;
using System;
using System.Threading.Tasks;

namespace Bible.Alarm.Services
{
    public abstract class ToastService : IToastService
    {
        public abstract Task ShowMessage(string message, int seconds = 2);

        public async Task ShowScheduledNotification(AlarmSchedule schedule, int seconds = 3)
        {
            var nextFire = schedule.NextFireDate();
            var timeSpan = nextFire - DateTimeOffset.Now;

            if (timeSpan.Days > 0)
            {
                await ShowMessage($"Alarm set for {timeSpan.Days} days, {timeSpan.Hours} hours and {timeSpan.Minutes} minutes from now.");
            }
            else
            {
                await ShowMessage($"Alarm set for {timeSpan.Hours} hours and {timeSpan.Minutes} minutes from now.");
            }

        }
        public void Dispose()
        {

        }

        public abstract Task Clear();

    }
}
