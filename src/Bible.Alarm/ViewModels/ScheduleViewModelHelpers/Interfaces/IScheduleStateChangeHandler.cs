#nullable enable
using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.ViewModels.ScheduleViewModelHelpers.Interfaces;

/// <summary>
/// Handles schedule state changes for music properties.
/// </summary>
public interface IScheduleStateChangeHandler
{
    bool HandleScheduleUpdateFromState(
        ScheduleStateItem? currentSchedule,
        ref string? lastMusicTrackCode,
        ref string? lastMusicPublicationCode,
        ref string? lastMusicLanguageCode,
        ref bool? lastMusicRepeat,
        out bool hasChanges);

    void UpdateMusicTrackingFields(
        ScheduleStateItem scheduleStateItem,
        ref string? lastMusicTrackCode,
        ref string? lastMusicPublicationCode,
        ref string? lastMusicLanguageCode,
        ref bool? lastMusicRepeat);

    void ResetMusicTrackingFields(
        ref string? lastMusicTrackCode,
        ref string? lastMusicPublicationCode,
        ref string? lastMusicLanguageCode,
        ref bool? lastMusicRepeat);
}
