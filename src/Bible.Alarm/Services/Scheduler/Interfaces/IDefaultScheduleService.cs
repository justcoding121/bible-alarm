#nullable enable
using Bible.Alarm.Services.Scheduler.Models;

namespace Bible.Alarm.Services.Scheduler.Interfaces;

/// <summary>
/// Service for getting the next schedule track metadata for Android Auto MediaSession.
/// Returns track metadata and scheduleId for the next schedule to be played.
/// </summary>
public interface IDefaultScheduleService
{
    /// <summary>
    /// Gets the next schedule track metadata.
    /// Selects scheduleId from Fluxor state (playback/current schedule/first schedule) and returns
    /// first track metadata for that schedule. Does not seed or create schedules.
    /// </summary>
    Task<ScheduleTrackMetadata> GetNextScheduleTrackMetaDataAsync();

    /// <summary>
    /// Validates if a schedule ID exists in the application state.
    /// </summary>
    bool ValidateScheduleIdExists(int scheduleId);

    /// <summary>
    /// Gets the first schedule ID from the application state.
    /// </summary>
    int? GetFirstScheduleId();
}

