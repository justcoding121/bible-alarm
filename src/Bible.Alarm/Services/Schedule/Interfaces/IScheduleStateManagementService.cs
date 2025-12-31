#nullable enable
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Services.Schedule.Interfaces;

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
        ref MusicType? lastMusicType,
        ref int? lastMusicTrackNumber,
        ref string? lastMusicPublicationCode,
        ref string? lastMusicLanguageCode,
        ref bool? lastMusicRepeat,
        out bool musicUpdated);
}

