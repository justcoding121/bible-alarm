#nullable enable
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Services.Schedule.Interfaces;

public interface IScheduleCommandService
{
    Task ExecuteCancelAsync(
        bool isNewSchedule,
        int scheduleId,
        ScheduleStateItem? currentSchedule);

    Task<bool> ExecuteSaveAsync(
        bool isNewSchedule,
        int scheduleId,
        ScheduleStateItem currentSchedule,
        bool musicUpdated,
        bool biblePublicationUpdated,
        bool modelInitialized);

    Task<bool> ExecuteDeleteAsync(
        bool isNewSchedule,
        int scheduleId,
        int scheduleCount);

    Task ValidateNotificationPermissionsAsync(ScheduleStateItem? currentSchedule);

    Task StopPlaybackIfNeededAsync(
        bool isNewSchedule,
        bool isPreparingOrPlaying,
        int scheduleId,
        int currentPlaybackScheduleId);

    Task HandleSaveResultAsync(
        bool saved,
        int scheduleId,
        bool isEnabled,
        AlarmSchedule model);
}


