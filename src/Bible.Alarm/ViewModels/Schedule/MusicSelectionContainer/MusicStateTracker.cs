#nullable enable
using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.ViewModels.Schedule.MusicSelectionContainer;

/// <summary>
/// Music type is no longer tracked - inferred from LanguageCode (null = melody, non-null = vocal).
/// </summary>
public sealed class MusicStateTracker
{
    private string? lastScheduleMusicTrackCode;
    private string? lastScheduleMusicSectionCode;
    private string? lastScheduleMusicPublicationCode;
    private string? lastScheduleMusicLanguageCode;
    private bool lastScheduleMusicRepeat;
    private bool? lastMusicEnabled;
    private string? lastBibleLanguageDirection;
    private string? lastMusicLanguageDirection;
    private string? lastMusicPublicationName;
    private string? lastMusicSectionName;
    private int? defaultMusicTriggeredForScheduleId;

    public string? LastScheduleMusicTrackCode => lastScheduleMusicTrackCode;
    public string? LastMusicSectionCode => lastScheduleMusicSectionCode;
    public string? LastScheduleMusicPublicationCode => lastScheduleMusicPublicationCode;
    public string? LastScheduleMusicLanguageCode => lastScheduleMusicLanguageCode;
    public bool LastScheduleMusicRepeat => lastScheduleMusicRepeat;
    public bool? LastMusicEnabled => lastMusicEnabled;
    public string? LastBibleLanguageDirection => lastBibleLanguageDirection;
    public string? LastMusicLanguageDirection => lastMusicLanguageDirection;

    public void InitializeFromSchedule(ScheduleStateItem? currentSchedule)
    {
        if (currentSchedule == null)
        {
            return;
        }

        lastScheduleMusicTrackCode = currentSchedule.MusicTrackCode;
        lastScheduleMusicSectionCode = currentSchedule.MusicSectionCode;
        lastScheduleMusicPublicationCode = currentSchedule.MusicPublicationCode;
        lastScheduleMusicLanguageCode = currentSchedule.MusicLanguageCode;
        lastScheduleMusicRepeat = currentSchedule.MusicRepeat ?? false;
        lastMusicEnabled = currentSchedule.MusicEnabled;
        lastBibleLanguageDirection = currentSchedule.BiblePublicationLanguageDirection;
        lastMusicLanguageDirection = currentSchedule.MusicLanguageDirection;
    }

    public void UpdateFromSchedule(ScheduleStateItem? currentSchedule)
    {
        if (currentSchedule == null)
        {
            return;
        }

        lastScheduleMusicTrackCode = currentSchedule.MusicTrackCode;
        lastScheduleMusicSectionCode = currentSchedule.MusicSectionCode;
        lastScheduleMusicPublicationCode = currentSchedule.MusicPublicationCode;
        lastScheduleMusicLanguageCode = currentSchedule.MusicLanguageCode;
        lastScheduleMusicRepeat = currentSchedule.MusicRepeat ?? false;
        lastMusicEnabled = currentSchedule.MusicEnabled;
        lastBibleLanguageDirection = currentSchedule.BiblePublicationLanguageDirection;
        lastMusicLanguageDirection = currentSchedule.MusicLanguageDirection;
        lastMusicPublicationName = currentSchedule.MusicPublicationName;
        lastMusicSectionName = currentSchedule.MusicSectionName;
    }

    public bool HasMusicPublicationNameChanged(ScheduleStateItem? currentSchedule)
    {
        if (currentSchedule == null)
        {
            return false;
        }

        return lastMusicPublicationName != currentSchedule.MusicPublicationName;
    }

    public bool HasMusicSectionNameChanged(ScheduleStateItem? currentSchedule)
    {
        if (currentSchedule == null)
        {
            return false;
        }

        return lastMusicSectionName != currentSchedule.MusicSectionName;
    }

    public (bool languageCodeChanged, bool publicationCodeChanged, bool sectionCodeChanged, bool trackCodeChanged, bool repeatChanged) DetectChanges(ScheduleStateItem? currentSchedule)
    {
        if (currentSchedule == null)
        {
            return (false, false, false, false, false);
        }

        var languageCodeChanged = lastScheduleMusicLanguageCode != currentSchedule.MusicLanguageCode;
        var publicationCodeChanged = lastScheduleMusicPublicationCode != currentSchedule.MusicPublicationCode;
        var sectionCodeChanged = lastScheduleMusicSectionCode != currentSchedule.MusicSectionCode;
        var trackCodeChanged = lastScheduleMusicTrackCode != currentSchedule.MusicTrackCode;
        var repeatChanged = lastScheduleMusicRepeat != (currentSchedule.MusicRepeat ?? false);

        return (languageCodeChanged, publicationCodeChanged, sectionCodeChanged, trackCodeChanged, repeatChanged);
    }

    public bool HasMusicEnabledChanged(ScheduleStateItem? currentSchedule)
    {
        if (currentSchedule == null)
        {
            return false;
        }

        return lastMusicEnabled != currentSchedule.MusicEnabled;
    }

    public void UpdateMusicEnabled(bool? value)
    {
        lastMusicEnabled = value;
    }

    public bool HasBibleLanguageDirectionChanged(ScheduleStateItem? currentSchedule)
    {
        if (currentSchedule == null)
        {
            return false;
        }

        return lastBibleLanguageDirection != currentSchedule.BiblePublicationLanguageDirection;
    }

    public void UpdateBibleLanguageDirection(string? value)
    {
        lastBibleLanguageDirection = value;
    }

    public bool HasMusicLanguageDirectionChanged(ScheduleStateItem? currentSchedule)
    {
        if (currentSchedule == null)
        {
            return false;
        }

        return lastMusicLanguageDirection != currentSchedule.MusicLanguageDirection;
    }

    public void UpdateMusicLanguageDirection(string? value)
    {
        lastMusicLanguageDirection = value;
    }

    public bool ShouldTriggerDefaultMusicForNullPublication(int scheduleId)
    {
        return defaultMusicTriggeredForScheduleId != scheduleId;
    }

    public void RecordDefaultMusicTriggered(int scheduleId)
    {
        defaultMusicTriggeredForScheduleId = scheduleId;
    }

}
