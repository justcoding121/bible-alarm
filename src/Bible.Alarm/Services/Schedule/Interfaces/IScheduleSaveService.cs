#nullable enable
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Services.Schedule.Interfaces;

public interface IScheduleSaveService
{
    AlarmSchedule PrepareModelForSave(
        ScheduleStateItem currentSchedule,
        bool isNewSchedule,
        bool musicUpdated);

    ScheduleStateItem PrepareScheduleStateItem(
        AlarmSchedule model,
        ScheduleStateItem currentSchedule,
        bool musicUpdated);
}

