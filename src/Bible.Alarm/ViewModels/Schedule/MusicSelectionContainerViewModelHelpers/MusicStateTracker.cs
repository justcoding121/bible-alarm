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

    public MusicType? LastScheduleMusicType => lastScheduleMusicType;
    public int? LastScheduleMusicTrackNumber => lastScheduleMusicTrackNumber;
    public string? LastScheduleMusicPublicationCode => lastScheduleMusicPublicationCode;
    public string? LastScheduleMusicLanguageCode => lastScheduleMusicLanguageCode;
    public bool LastScheduleMusicRepeat => lastScheduleMusicRepeat;
    public bool? LastMusicEnabled => lastMusicEnabled;

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
}
