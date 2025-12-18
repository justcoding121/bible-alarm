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
}

