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
    /// When <paramref name="existingFromState"/> is provided and the schedule has a no-language publication (e.g. iam),
    /// the loaded item's language display is preferred from <paramref name="existingFromState"/> if it differs (e.g. state has MY, DB has E)
    /// so View Schedule shows the same language as the home list.
    /// </summary>
    Task<ScheduleStateItem?> LoadExistingScheduleAsync(int scheduleId, bool isEnabled, ScheduleStateItem? existingFromState = null);

    Task CompleteScheduleLoadAsync();

    void InitializeTrackingFields(
        ScheduleStateItem scheduleStateItem,
        ref int lastScheduleId,
        ref string? lastMusicTrackCode,
        ref string? lastMusicPublicationCode,
        ref string? lastMusicLanguageCode,
        ref bool? lastMusicRepeat);
}

