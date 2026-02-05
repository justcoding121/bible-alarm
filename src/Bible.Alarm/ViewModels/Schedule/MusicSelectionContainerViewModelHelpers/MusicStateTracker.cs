#nullable enable
using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.ViewModels.Schedule.MusicSelectionContainerViewModelHelpers;

/// <summary>
/// Tracks last values from CurrentSchedule to detect changes.
/// Separated from MusicSelectionContainerViewModel for better modularity.
/// Music type is no longer tracked - inferred from LanguageCode (null = melody, non-null = vocal).
/// </summary>
public sealed class MusicStateTracker
{
    // Track last values from CurrentSchedule to detect changes
    private int? lastScheduleMusicTrackNumber;
    private string? lastScheduleMusicSectionCode;
    private string? lastScheduleMusicPublicationCode;
    private string? lastScheduleMusicLanguageCode;
    private bool lastScheduleMusicRepeat;
    private bool? lastMusicEnabled;
    private string? lastBibleLanguageDirection;
    private string? lastMusicLanguageDirection;
    private string? lastMusicPublicationName;
    private string? lastMusicSectionName;

    public int? LastScheduleMusicTrackNumber => lastScheduleMusicTrackNumber;
    public string? LastMusicSectionCode => lastScheduleMusicSectionCode;
    public string? LastScheduleMusicPublicationCode => lastScheduleMusicPublicationCode;
    public string? LastScheduleMusicLanguageCode => lastScheduleMusicLanguageCode;
    public bool LastScheduleMusicRepeat => lastScheduleMusicRepeat;
    public bool? LastMusicEnabled => lastMusicEnabled;
    public string? LastBibleLanguageDirection => lastBibleLanguageDirection;
    public string? LastMusicLanguageDirection => lastMusicLanguageDirection;

    /// <summary>
    /// Initializes tracking values from CurrentSchedule.
    /// </summary>
    public void InitializeFromSchedule(ScheduleStateItem? currentSchedule)
    {
        if (currentSchedule == null)
        {
            return;
        }

        lastScheduleMusicTrackNumber = currentSchedule.MusicTrackNumber;
        lastScheduleMusicSectionCode = currentSchedule.MusicSectionCode;
        lastScheduleMusicPublicationCode = currentSchedule.MusicPublicationCode;
        lastScheduleMusicLanguageCode = currentSchedule.MusicLanguageCode;
        lastScheduleMusicRepeat = currentSchedule.MusicRepeat ?? false;
        lastMusicEnabled = currentSchedule.MusicEnabled;
        lastBibleLanguageDirection = currentSchedule.BiblePublicationLanguageDirection;
        lastMusicLanguageDirection = currentSchedule.MusicLanguageDirection;
    }

    /// <summary>
    /// Updates tracking values from CurrentSchedule.
    /// </summary>
    public void UpdateFromSchedule(ScheduleStateItem? currentSchedule)
    {
        if (currentSchedule == null)
        {
            return;
        }

        lastScheduleMusicTrackNumber = currentSchedule.MusicTrackNumber;
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

    /// <summary>
    /// Checks if MusicPublicationName changed (display name, not code).
    /// </summary>
    public bool HasMusicPublicationNameChanged(ScheduleStateItem? currentSchedule)
    {
        if (currentSchedule == null)
        {
            return false;
        }

        return lastMusicPublicationName != currentSchedule.MusicPublicationName;
    }

    /// <summary>
    /// Checks if MusicSectionName changed (display name, not code).
    /// </summary>
    public bool HasMusicSectionNameChanged(ScheduleStateItem? currentSchedule)
    {
        if (currentSchedule == null)
        {
            return false;
        }

        return lastMusicSectionName != currentSchedule.MusicSectionName;
    }

    /// <summary>
    /// Detects changes in music properties from CurrentSchedule.
    /// Returns tuple without musicTypeChanged (no longer used).
    /// </summary>
    public (bool languageCodeChanged, bool publicationCodeChanged, bool sectionCodeChanged, bool trackNumberChanged, bool repeatChanged) DetectChanges(ScheduleStateItem? currentSchedule)
    {
        if (currentSchedule == null)
        {
            return (false, false, false, false, false);
        }

        var languageCodeChanged = lastScheduleMusicLanguageCode != currentSchedule.MusicLanguageCode;
        var publicationCodeChanged = lastScheduleMusicPublicationCode != currentSchedule.MusicPublicationCode;
        var sectionCodeChanged = lastScheduleMusicSectionCode != currentSchedule.MusicSectionCode;
        var trackNumberChanged = lastScheduleMusicTrackNumber != currentSchedule.MusicTrackNumber;
        var repeatChanged = lastScheduleMusicRepeat != (currentSchedule.MusicRepeat ?? false);

        return (languageCodeChanged, publicationCodeChanged, sectionCodeChanged, trackNumberChanged, repeatChanged);
    }

    /// <summary>
    /// Checks if MusicEnabled changed.
    /// </summary>
    public bool HasMusicEnabledChanged(ScheduleStateItem? currentSchedule)
    {
        if (currentSchedule == null)
        {
            return false;
        }

        return lastMusicEnabled != currentSchedule.MusicEnabled;
    }

    /// <summary>
    /// Updates only the MusicEnabled tracking value.
    /// </summary>
    public void UpdateMusicEnabled(bool? value)
    {
        lastMusicEnabled = value;
    }

    /// <summary>
    /// Checks if Bible language direction changed (affects RTL/LTR layout).
    /// </summary>
    public bool HasBibleLanguageDirectionChanged(ScheduleStateItem? currentSchedule)
    {
        if (currentSchedule == null)
        {
            return false;
        }

        return lastBibleLanguageDirection != currentSchedule.BiblePublicationLanguageDirection;
    }

    /// <summary>
    /// Updates only the Bible language direction tracking value.
    /// </summary>
    public void UpdateBibleLanguageDirection(string? value)
    {
        lastBibleLanguageDirection = value;
    }

    /// <summary>
    /// Checks if Music language direction changed (affects RTL/LTR layout for music rows).
    /// </summary>
    public bool HasMusicLanguageDirectionChanged(ScheduleStateItem? currentSchedule)
    {
        if (currentSchedule == null)
        {
            return false;
        }

        return lastMusicLanguageDirection != currentSchedule.MusicLanguageDirection;
    }

    /// <summary>
    /// Updates only the Music language direction tracking value.
    /// </summary>
    public void UpdateMusicLanguageDirection(string? value)
    {
        lastMusicLanguageDirection = value;
    }
}
