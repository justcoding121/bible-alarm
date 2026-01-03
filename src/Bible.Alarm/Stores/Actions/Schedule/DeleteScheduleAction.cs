namespace Bible.Alarm.Stores.Actions.Schedule;

/// <summary>
/// Action dispatched from UI/ViewModel when user wants to delete a schedule.
/// Contains only the schedule ID (domain model identifier).
/// Following Fluxor best practices: Actions contain domain model identifiers.
/// </summary>
public record DeleteScheduleAction(int ScheduleId);

