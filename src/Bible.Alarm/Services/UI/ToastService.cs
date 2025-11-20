using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Models.Schedule;

namespace Bible.Alarm.Services.UI;

public abstract class ToastService : IToastService
{
    public abstract Task ShowMessage(string message, int seconds = 2);

    public async Task ShowScheduledNotification(AlarmSchedule schedule, int seconds = 3)
    {
        var nextFire = schedule.NextFireDate();
        var timeSpan = nextFire - DateTimeOffset.Now;

        if (timeSpan.Days > 0)
            await ShowMessage(
                $"Alarm set for {timeSpan.Days} days, {timeSpan.Hours} hours and {timeSpan.Minutes} minutes from now.");
        else
            await ShowMessage($"Alarm set for {timeSpan.Hours} hours and {timeSpan.Minutes} minutes from now.");
    }

    public void Dispose()
    {
    }

    public abstract Task Clear();
}