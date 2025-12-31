#nullable enable
using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Services.Schedule.Interfaces;

public interface IScheduleLoadService
{
    Task LoadScheduleFromStateAsync(
        ScheduleStateItem currentScheduleItem,
        int currentScheduleId,
        Action<int> onTrackingFieldsInitialized,
        Func<Task> onLoadComplete);

    Task InitializeNewScheduleAsync(
        Func<bool> isInitializingCheck,
        Func<bool> modelInitializedCheck,
        Func<ScheduleStateItem?> getCurrentSchedule,
        Action<ScheduleStateItem, int> onScheduleInitialized,
        Action onLoadComplete);
}

