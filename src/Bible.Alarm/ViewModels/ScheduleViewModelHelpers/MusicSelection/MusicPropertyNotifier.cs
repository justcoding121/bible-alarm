#nullable enable
using Bible.Alarm.ViewModels.Schedule;

namespace Bible.Alarm.ViewModels.ScheduleViewModelHelpers.MusicSelection;

/// <summary>
/// Handles cascading property change notifications for music properties.
/// Separated from MusicSelectionContainerViewModel for better modularity.
/// Music type is inferred from LanguageCode: NULL/empty = instrumental (melody), otherwise = vocal.
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
    /// isMelodyMusic = true means instrumental (no language), false means vocal (has language).
    /// </summary>
    public void NotifyPropertiesChanged(
        bool languageCodeChanged,
        bool publicationCodeChanged,
        bool sectionCodeChanged,
        bool trackNumberChanged,
        bool repeatChanged,
        bool isMelodyMusic,
        Action<bool>? setShouldScrollToBottom = null)
    {
        // Determine which properties need to be notified (cascading logic)
        var notifyLanguage = languageCodeChanged;
        var notifySongPublication = languageCodeChanged || publicationCodeChanged;
        var notifySection = languageCodeChanged || publicationCodeChanged || sectionCodeChanged;
        var notifyTrack = languageCodeChanged || publicationCodeChanged || sectionCodeChanged || trackNumberChanged;

        // Language change cascades to all below (including flow direction for RTL support)
        if (notifyLanguage)
        {
            onPropertyChanged(nameof(MusicSelectionContainerViewModel.ContentFlowDirection));
            onPropertyChanged(nameof(MusicSelectionContainerViewModel.IsMusicLanguageVisible));
            onPropertyChanged(nameof(MusicSelectionContainerViewModel.MusicLanguageDisplayText));
            onPropertyChanged(nameof(MusicSelectionContainerViewModel.IsSongPublicationVisible));
            onPropertyChanged(nameof(MusicSelectionContainerViewModel.SongPublicationDisplayText));
            onPropertyChanged(nameof(MusicSelectionContainerViewModel.IsMusicSectionVisible));
            onPropertyChanged(nameof(MusicSelectionContainerViewModel.MusicSectionDisplayText));
            onPropertyChanged(nameof(MusicSelectionContainerViewModel.TrackDisplayText));

            // Clear cached values when language changes
            displayTextProvider.ClearCaches();

            // Signal to scroll to bottom when user selects vocals (only if music is enabled and has language)
            if (!isMelodyMusic)
            {
                setShouldScrollToBottom?.Invoke(true);
            }
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
        if (notifyLanguage || notifySongPublication || notifySection || notifyTrack || repeatChanged)
        {
            onPropertyChanged(nameof(MusicSelectionContainerViewModel.IsRepeatEnabled));
            onPropertyChanged(nameof(MusicSelectionContainerViewModel.HasTrackSelected));
        }
    }
}

