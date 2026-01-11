#nullable enable
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Services.Schedule.Interfaces;

public interface IScheduleDisplayNameService
{
    Task PopulateDisplayNamesAsync(ScheduleStateItem scheduleStateItem, AlarmSchedule schedule);
}


