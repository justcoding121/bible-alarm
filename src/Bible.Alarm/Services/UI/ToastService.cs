using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Models.Schedule;

namespace Bible.Alarm.Services.UI;

public abstract class ToastService : IToastService
{
    private bool disposed;

    public abstract Task ShowMessage(string message, int seconds = 3);

    public async Task ShowScheduledNotification(AlarmSchedule schedule, int seconds = 3)
    {
        var nextFire = schedule.NextFireDate();
        var timeSpan = nextFire - DateTimeOffset.Now;

        if (timeSpan.Days > 0)
        {
            await ShowMessage(
                $"Reminder set for {timeSpan.Days} days, {timeSpan.Hours} hours and {timeSpan.Minutes} minutes from now",
                seconds);
        }
        else
        {
            await ShowMessage($"Reminder set for {timeSpan.Hours} hours and {timeSpan.Minutes} minutes from now", seconds);
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposed)
        {
            return;
        }

        if (disposing)
        {
            // No managed IDisposable resources tracked by the base abstraction.
        }

        disposed = true;
    }

    public abstract Task Clear();
}
