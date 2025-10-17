using Bible.Alarm.Models;
using System;
using System.Threading.Tasks;

namespace Bible.Alarm.Services.Contracts
{
    public interface IToastService : IDisposable
    {
        Task ShowMessage(string message, int seconds = 3);
        Task ShowScheduledNotification(AlarmSchedule schedule, int seconds = 3);
        Task Clear();
    }
}
