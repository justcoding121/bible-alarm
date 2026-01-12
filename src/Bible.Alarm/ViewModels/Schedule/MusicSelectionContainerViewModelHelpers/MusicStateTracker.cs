#nullable enable
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.ViewModels.Schedule.MusicSelectionContainerViewModelHelpers;

/// <summary>
/// Tracks last values from CurrentSchedule to detect changes.
/// Separated from MusicSelectionContainerViewModel for better modularity.
/// </summary>
public sealed class MusicStateTracker
{
    // Track last values from CurrentSchedule to detect changes
    private MusicType? lastScheduleMusicType;
    private int? lastScheduleMusicTrackNumber;
    private string? lastScheduleMusicPublicationCode;
    private string? lastScheduleMusicLanguageCode;
    private bool lastScheduleMusicRepeat;
    private bool? lastMusicEnabled;
    private string? lastBibleLanguageDirection;
    private string? lastMusicLanguageDirection;

    public MusicType? LastScheduleMusicType => lastScheduleMusicType;
    public int? LastScheduleMusicTrackNumber => lastScheduleMusicTrackNumber;
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

        lastScheduleMusicType = currentSchedule.MusicType;
        lastScheduleMusicTrackNumber = currentSchedule.MusicTrackNumber;
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

        lastScheduleMusicType = currentSchedule.MusicType;
        lastScheduleMusicTrackNumber = currentSchedule.MusicTrackNumber;
        lastScheduleMusicPublicationCode = currentSchedule.MusicPublicationCode;
        lastScheduleMusicLanguageCode = currentSchedule.MusicLanguageCode;
        lastScheduleMusicRepeat = currentSchedule.MusicRepeat ?? false;
        lastMusicEnabled = currentSchedule.MusicEnabled;
        lastBibleLanguageDirection = currentSchedule.BiblePublicationLanguageDirection;
        lastMusicLanguageDirection = currentSchedule.MusicLanguageDirection;
    }

    /// <summary>
    /// Detects changes in music properties from CurrentSchedule.
    /// </summary>
    public (bool musicTypeChanged, bool languageCodeChanged, bool publicationCodeChanged, bool trackNumberChanged, bool repeatChanged) DetectChanges(ScheduleStateItem? currentSchedule)
    {
        if (currentSchedule == null)
        {
            return (false, false, false, false, false);
        }

        var musicTypeChanged = lastScheduleMusicType != currentSchedule.MusicType;
        var languageCodeChanged = lastScheduleMusicLanguageCode != currentSchedule.MusicLanguageCode;
        var publicationCodeChanged = lastScheduleMusicPublicationCode != currentSchedule.MusicPublicationCode;
        var trackNumberChanged = lastScheduleMusicTrackNumber != currentSchedule.MusicTrackNumber;
        var repeatChanged = lastScheduleMusicRepeat != (currentSchedule.MusicRepeat ?? false);

        return (musicTypeChanged, languageCodeChanged, publicationCodeChanged, trackNumberChanged, repeatChanged);
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
