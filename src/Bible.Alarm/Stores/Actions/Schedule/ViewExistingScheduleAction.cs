#nullable enable

namespace Bible.Alarm.Stores.Actions.Schedule;

/// <summary>
/// Action to view an existing schedule by loading fresh data from the database.
/// Unlike ViewScheduleAction which uses pre-loaded data, this triggers a DB load on background thread.
/// </summary>
public class ViewExistingScheduleAction(int scheduleId, bool isEnabled)
{
    public int ScheduleId { get; } = scheduleId;
    public bool IsEnabled { get; } = isEnabled;
}
