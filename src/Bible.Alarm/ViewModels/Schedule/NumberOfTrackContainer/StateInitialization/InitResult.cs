#nullable enable

namespace Bible.Alarm.ViewModels.Schedule.NumberOfTrackContainer.StateInitialization;

/// <summary>
/// Result of initializing NumberOfTrackContainerViewModel from ApplicationState.
/// </summary>
public sealed record InitResult(
    int ScheduleId,
    bool NotificationEnabled,
    bool AlwaysPlayFromStart,
    bool PlayIndefinitely,
    string? LastCategoryName);
