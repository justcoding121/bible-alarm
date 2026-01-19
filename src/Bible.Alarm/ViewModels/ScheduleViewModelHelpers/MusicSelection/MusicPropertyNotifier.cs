#nullable enable
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.ViewModels.Schedule;

namespace Bible.Alarm.ViewModels.ScheduleViewModelHelpers.MusicSelection;

/// <summary>
/// Handles cascading property change notifications for music properties.
/// Separated from MusicSelectionContainerViewModel for better modularity.
/// </summary>
public sealed class MusicPropertyNotifier
{
    private readonly Action<string> onPropertyChanged;
    private readonly MusicDisplayTextProvider displayTextProvider;

    public MusicPropertyNotifier(Action<string> onPropertyChanged, MusicDisplayTextProvider displayTextProvider)
    {
        this.onPropertyChanged = onPropertyChanged ?? throw new ArgumentNullException(nameof(onPropertyChanged));
        this.displayTextProvider = displayTextProvider;
    }

    /// <summary>
    /// Notifies all music-related properties changed in a single batch.
    /// This reduces UI thread work compared to individual notifications.
    /// </summary>
    public void NotifyAllMusicPropertiesChanged()
    {
        onPropertyChanged(nameof(MusicSelectionContainerViewModel.MusicEnabled));
        onPropertyChanged(nameof(MusicSelectionContainerViewModel.MusicTypeDisplayText));
        onPropertyChanged(nameof(MusicSelectionContainerViewModel.IsMusicLanguageVisible));
        onPropertyChanged(nameof(MusicSelectionContainerViewModel.MusicLanguageDisplayText));
        onPropertyChanged(nameof(MusicSelectionContainerViewModel.IsSongPublicationVisible));
        onPropertyChanged(nameof(MusicSelectionContainerViewModel.SongPublicationDisplayText));
        onPropertyChanged(nameof(MusicSelectionContainerViewModel.IsMusicSectionVisible));
        onPropertyChanged(nameof(MusicSelectionContainerViewModel.MusicSectionDisplayText));
        onPropertyChanged(nameof(MusicSelectionContainerViewModel.TrackDisplayText));
        onPropertyChanged(nameof(MusicSelectionContainerViewModel.IsRepeatEnabled));
        onPropertyChanged(nameof(MusicSelectionContainerViewModel.HasTrackSelected));
        onPropertyChanged(nameof(MusicSelectionContainerViewModel.ContentFlowDirection));
    }

    /// <summary>
    /// Notifies that the ContentFlowDirection property has changed.
    /// Called when the music's language direction changes.
    /// </summary>
    public void NotifyFlowDirectionChanged()
    {
        onPropertyChanged(nameof(MusicSelectionContainerViewModel.ContentFlowDirection));
    }

    /// <summary>
    /// Notifies properties based on what changed, using cascading logic.
    /// </summary>
    public void NotifyPropertiesChanged(
        bool musicTypeChanged,
        bool languageCodeChanged,
        bool publicationCodeChanged,
        bool sectionNumberChanged,
        bool trackNumberChanged,
        bool repeatChanged,
        MusicType? musicType,
        Action<bool>? setShouldScrollToBottom = null)
    {
        // Determine which properties need to be notified (cascading logic)
        var notifyMusicType = musicTypeChanged;
        var notifyLanguage = musicTypeChanged || languageCodeChanged;
        var notifySongPublication = musicTypeChanged || languageCodeChanged || publicationCodeChanged;
        var notifySection = musicTypeChanged || languageCodeChanged || publicationCodeChanged || sectionNumberChanged;
        var notifyTrack = musicTypeChanged || languageCodeChanged || publicationCodeChanged || sectionNumberChanged || trackNumberChanged;

        // Music type change cascades to all below (including flow direction for RTL support)
        if (notifyMusicType)
        {
            onPropertyChanged(nameof(MusicSelectionContainerViewModel.MusicTypeDisplayText));
            onPropertyChanged(nameof(MusicSelectionContainerViewModel.IsMusicLanguageVisible));
            onPropertyChanged(nameof(MusicSelectionContainerViewModel.IsSongPublicationVisible));
            onPropertyChanged(nameof(MusicSelectionContainerViewModel.IsMusicSectionVisible));
            onPropertyChanged(nameof(MusicSelectionContainerViewModel.ContentFlowDirection));

            // Clear cached values when music type changes
            displayTextProvider.ClearCaches();

            // For vocals: notify language, music publication, section, and track
            // For melodies: only notify track
            if (musicType == MusicType.VocalMusic)
            {
                onPropertyChanged(nameof(MusicSelectionContainerViewModel.MusicLanguageDisplayText));
                onPropertyChanged(nameof(MusicSelectionContainerViewModel.SongPublicationDisplayText));
                onPropertyChanged(nameof(MusicSelectionContainerViewModel.MusicSectionDisplayText));

                // Signal to scroll to bottom when user selects vocals (only if music is enabled)
                setShouldScrollToBottom?.Invoke(true);
            }
            onPropertyChanged(nameof(MusicSelectionContainerViewModel.TrackDisplayText));
        }
        // Language change (vocals only) cascades to music publication, section, track, and flow direction
        else if (notifyLanguage && musicType == MusicType.VocalMusic)
        {
            onPropertyChanged(nameof(MusicSelectionContainerViewModel.ContentFlowDirection));
            onPropertyChanged(nameof(MusicSelectionContainerViewModel.MusicLanguageDisplayText));
            onPropertyChanged(nameof(MusicSelectionContainerViewModel.SongPublicationDisplayText));
            onPropertyChanged(nameof(MusicSelectionContainerViewModel.IsMusicSectionVisible));
            onPropertyChanged(nameof(MusicSelectionContainerViewModel.MusicSectionDisplayText));
            onPropertyChanged(nameof(MusicSelectionContainerViewModel.TrackDisplayText));

            // Clear music publication, section, and track caches when language changes
            displayTextProvider.ClearSongPublicationCache();
            displayTextProvider.ClearTrackCache();
        }
        // Music publication change cascades to section and track
        else if (notifySongPublication)
        {
            onPropertyChanged(nameof(MusicSelectionContainerViewModel.SongPublicationDisplayText));
            onPropertyChanged(nameof(MusicSelectionContainerViewModel.IsMusicSectionVisible));
            onPropertyChanged(nameof(MusicSelectionContainerViewModel.MusicSectionDisplayText));
            onPropertyChanged(nameof(MusicSelectionContainerViewModel.TrackDisplayText));

            // Clear section and track caches when music publication changes
            displayTextProvider.ClearTrackCache();
        }
        // Section change cascades to track
        else if (notifySection)
        {
            onPropertyChanged(nameof(MusicSelectionContainerViewModel.MusicSectionDisplayText));
            onPropertyChanged(nameof(MusicSelectionContainerViewModel.TrackDisplayText));

            // Clear track cache when section changes
            displayTextProvider.ClearTrackCache();
        }
        // Track change only affects itself
        else if (notifyTrack)
        {
            onPropertyChanged(nameof(MusicSelectionContainerViewModel.TrackDisplayText));

            // Clear track cache when track changes
            displayTextProvider.ClearTrackCache();
        }

        // Always notify repeat and has track selected if any music property changed
        if (notifyMusicType || notifyLanguage || notifySongPublication || notifySection || notifyTrack || repeatChanged)
        {
            onPropertyChanged(nameof(MusicSelectionContainerViewModel.IsRepeatEnabled));
            onPropertyChanged(nameof(MusicSelectionContainerViewModel.HasTrackSelected));
        }
    }
}

