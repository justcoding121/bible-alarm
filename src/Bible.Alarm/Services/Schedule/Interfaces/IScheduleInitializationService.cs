#nullable enable
using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Services.Schedule.Interfaces;

/// <summary>
/// Service for initializing schedule state.
/// Music type is inferred from LanguageCode: NULL/empty = instrumental (melody), otherwise = vocal.
/// </summary>
public interface IScheduleInitializationService
{
    Task<ScheduleStateItem> InitializeNewScheduleAsync();

    /// <summary>
    /// Loads an existing schedule from the database and maps it to a ScheduleStateItem.
    /// </summary>
    Task<ScheduleStateItem?> LoadExistingScheduleAsync(int scheduleId, bool isEnabled);

    Task CompleteScheduleLoadAsync();

    void InitializeTrackingFields(
        ScheduleStateItem scheduleStateItem,
        ref int lastScheduleId,
        ref int? lastMusicTrackNumber,
        ref string? lastMusicPublicationCode,
        ref string? lastMusicLanguageCode,
        ref bool? lastMusicRepeat);
}

