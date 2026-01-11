#nullable enable
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Services.Schedule.Interfaces;

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
        ref MusicType? lastMusicType,
        ref int? lastMusicTrackNumber,
        ref string? lastMusicPublicationCode,
        ref string? lastMusicLanguageCode,
        ref bool? lastMusicRepeat);
}

