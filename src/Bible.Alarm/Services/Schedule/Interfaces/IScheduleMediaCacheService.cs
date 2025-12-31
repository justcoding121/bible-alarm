#nullable enable

namespace Bible.Alarm.Services.Schedule.Interfaces;

public interface IScheduleMediaCacheService
{
    void SetupMediaCache(int scheduleId, bool isUpdate = false);
}

