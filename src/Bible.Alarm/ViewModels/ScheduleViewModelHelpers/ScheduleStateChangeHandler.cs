#nullable enable
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Stores.Models;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.ScheduleViewModelHelpers;

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
        ref MusicType? lastMusicType,
        ref int? lastMusicTrackNumber,
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
        var musicTypeChanged = lastMusicType != currentSchedule.MusicType;
        var musicTrackChanged = lastMusicTrackNumber != currentSchedule.MusicTrackNumber;
        var musicPublicationChanged = lastMusicPublicationCode != currentSchedule.MusicPublicationCode;
        var musicLanguageChanged = lastMusicLanguageCode != currentSchedule.MusicLanguageCode;
        var musicRepeatChanged = lastMusicRepeat != currentSchedule.MusicRepeat;

        if (musicTypeChanged || musicTrackChanged || musicPublicationChanged || musicLanguageChanged || musicRepeatChanged)
        {
            logger.Debug("HandleScheduleUpdateFromState: Music changed. Type: {OldType} -> {NewType}, Track: {OldTrack} -> {NewTrack}, Publication: {OldPub} -> {NewPub}, Language: {OldLang} -> {NewLang}, Repeat: {OldRepeat} -> {NewRepeat}",
                lastMusicType, currentSchedule.MusicType,
                lastMusicTrackNumber, currentSchedule.MusicTrackNumber,
                lastMusicPublicationCode, currentSchedule.MusicPublicationCode,
                lastMusicLanguageCode, currentSchedule.MusicLanguageCode,
                lastMusicRepeat, currentSchedule.MusicRepeat);

            hasChanges = true;

            // Update tracking fields
            lastMusicType = currentSchedule.MusicType;
            lastMusicTrackNumber = currentSchedule.MusicTrackNumber;
            lastMusicPublicationCode = currentSchedule.MusicPublicationCode;
            lastMusicLanguageCode = currentSchedule.MusicLanguageCode;
            lastMusicRepeat = currentSchedule.MusicRepeat;
        }

        return hasChanges;
    }

    public void UpdateMusicTrackingFields(
        ScheduleStateItem scheduleStateItem,
        ref MusicType? lastMusicType,
        ref int? lastMusicTrackNumber,
        ref string? lastMusicPublicationCode,
        ref string? lastMusicLanguageCode,
        ref bool? lastMusicRepeat)
    {
        lastMusicType = scheduleStateItem.MusicType;
        lastMusicTrackNumber = scheduleStateItem.MusicTrackNumber;
        lastMusicPublicationCode = scheduleStateItem.MusicPublicationCode;
        lastMusicLanguageCode = scheduleStateItem.MusicLanguageCode;
        lastMusicRepeat = scheduleStateItem.MusicRepeat;
    }

    public void ResetMusicTrackingFields(
        ref MusicType? lastMusicType,
        ref int? lastMusicTrackNumber,
        ref string? lastMusicPublicationCode,
        ref string? lastMusicLanguageCode,
        ref bool? lastMusicRepeat)
    {
        lastMusicType = null;
        lastMusicTrackNumber = null;
        lastMusicPublicationCode = null;
        lastMusicLanguageCode = null;
        lastMusicRepeat = null;
    }
}

