using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Models.Schedule;

namespace Bible.Alarm.Services.UI;

public abstract class ToastService : IToastService, IDisposable
{
    private bool disposed;

    public abstract Task ShowMessage(string message, int seconds = 2);

    public async Task ShowScheduledNotification(AlarmSchedule schedule, int seconds = 3)
    {
        var nextFire = schedule.NextFireDate();
        var timeSpan = nextFire - DateTimeOffset.Now;

        if (timeSpan.Days > 0)
        {
            await ShowMessage(
                $"Reminder set for {timeSpan.Days} days, {timeSpan.Hours} hours and {timeSpan.Minutes} minutes from now");
        }
        else
        {
            await ShowMessage($"Reminder set for {timeSpan.Hours} hours and {timeSpan.Minutes} minutes from now");
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
        }

        disposed = true;
    }

    public abstract Task Clear();
}
