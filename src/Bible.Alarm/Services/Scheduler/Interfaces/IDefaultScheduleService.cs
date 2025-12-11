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
    /// Returns the first track metadata from the most recently played schedule if valid.
    /// Otherwise, creates a sample schedule, saves it to database, and returns its first track metadata.
    /// </summary>
    Task<ScheduleTrackMetadata> GetNextScheduleTrackMetaDataAsync();
}

