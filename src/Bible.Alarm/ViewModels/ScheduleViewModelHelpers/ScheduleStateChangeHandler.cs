#nullable enable
using Bible.Alarm.Stores.Models;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.ScheduleViewModelHelpers;

/// <summary>
/// Handles schedule state changes for music properties.
/// Music type is inferred from LanguageCode: NULL/empty = instrumental, otherwise = vocal.
/// </summary>
public class ScheduleStateChangeHandler
{
    private readonly ILogger logger;
    private readonly IDispatcher dispatcher;

    public ScheduleStateChangeHandler(ILogger logger, IDispatcher dispatcher)
    {
        this.logger = logger;
        this.dispatcher = dispatcher;
    }

    public bool HandleScheduleUpdateFromState(
        ScheduleStateItem? currentSchedule,
        ref string? lastMusicTrackCode,
        ref string? lastMusicPublicationCode,
        ref string? lastMusicLanguageCode,
        ref bool? lastMusicRepeat,
        out bool hasChanges)
    {
        hasChanges = false;

        if (currentSchedule == null)
        {
            return false;
        }

        // Check if music properties changed
        var musicTrackChanged = lastMusicTrackCode != currentSchedule.MusicTrackCode;
        var musicPublicationChanged = lastMusicPublicationCode != currentSchedule.MusicPublicationCode;
        var musicLanguageChanged = lastMusicLanguageCode != currentSchedule.MusicLanguageCode;
        var musicRepeatChanged = lastMusicRepeat != currentSchedule.MusicRepeat;

        if (musicTrackChanged || musicPublicationChanged || musicLanguageChanged || musicRepeatChanged)
        {
            logger.Debug("HandleScheduleUpdateFromState: Music changed. Track: {OldTrack} -> {NewTrack}, Publication: {OldPub} -> {NewPub}, Language: {OldLang} -> {NewLang}, Repeat: {OldRepeat} -> {NewRepeat}",
                lastMusicTrackCode, currentSchedule.MusicTrackCode,
                lastMusicPublicationCode, currentSchedule.MusicPublicationCode,
                lastMusicLanguageCode, currentSchedule.MusicLanguageCode,
                lastMusicRepeat, currentSchedule.MusicRepeat);

            hasChanges = true;

            // Update tracking fields
            lastMusicTrackCode = currentSchedule.MusicTrackCode;
            lastMusicPublicationCode = currentSchedule.MusicPublicationCode;
            lastMusicLanguageCode = currentSchedule.MusicLanguageCode;
            lastMusicRepeat = currentSchedule.MusicRepeat;
        }

        return hasChanges;
    }

    public void UpdateMusicTrackingFields(
        ScheduleStateItem scheduleStateItem,
        ref string? lastMusicTrackCode,
        ref string? lastMusicPublicationCode,
        ref string? lastMusicLanguageCode,
        ref bool? lastMusicRepeat)
    {
        lastMusicTrackCode = scheduleStateItem.MusicTrackCode;
        lastMusicPublicationCode = scheduleStateItem.MusicPublicationCode;
        lastMusicLanguageCode = scheduleStateItem.MusicLanguageCode;
        lastMusicRepeat = scheduleStateItem.MusicRepeat;
    }

    public void ResetMusicTrackingFields(
        ref string? lastMusicTrackCode,
        ref string? lastMusicPublicationCode,
        ref string? lastMusicLanguageCode,
        ref bool? lastMusicRepeat)
    {
        lastMusicTrackCode = null;
        lastMusicPublicationCode = null;
        lastMusicLanguageCode = null;
        lastMusicRepeat = null;
    }
}

