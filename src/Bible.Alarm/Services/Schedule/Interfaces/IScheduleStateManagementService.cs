#nullable enable
using Bible.Alarm.Stores;

namespace Bible.Alarm.Services.Schedule.Interfaces;

/// <summary>
/// Service for managing schedule state changes.
/// Music type is inferred from LanguageCode: NULL/empty = instrumental (melody), otherwise = vocal.
/// </summary>
public interface IScheduleStateManagementService
{
    void HandleStateChanged(
        ApplicationState stateValue,
        ref int lastScheduleId,
        ref bool modelInitialized,
        ref bool isSchedulePageOverlayVisible,
        Action<int> onScheduleChanged,
        Action onPropertyChanged);

    bool HandleScheduleUpdateFromState(
        ApplicationState stateValue,
        int currentScheduleId,
        ref int? lastMusicTrackNumber,
        ref string? lastMusicPublicationCode,
        ref string? lastMusicLanguageCode,
        ref bool? lastMusicRepeat,
        out bool musicUpdated);
}

