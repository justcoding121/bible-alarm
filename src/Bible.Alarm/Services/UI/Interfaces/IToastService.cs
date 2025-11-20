using Bible.Alarm.Models.Schedule;

namespace Bible.Alarm.Services.UI.Interfaces;

public interface IToastService : IDisposable
{
    Task ShowMessage(string message, int seconds = 3);
    Task ShowScheduledNotification(AlarmSchedule schedule, int seconds = 3);
    Task Clear();
}

