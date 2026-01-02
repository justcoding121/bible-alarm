#nullable enable
using Bible;
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
        onPropertyChanged(nameof(MusicSelectionContainerViewModel.IsSongBookVisible));
        onPropertyChanged(nameof(MusicSelectionContainerViewModel.SongBookDisplayText));
        onPropertyChanged(nameof(MusicSelectionContainerViewModel.TrackDisplayText));
        onPropertyChanged(nameof(MusicSelectionContainerViewModel.IsRepeatEnabled));
        onPropertyChanged(nameof(MusicSelectionContainerViewModel.HasTrackSelected));
    }

    /// <summary>
    /// Notifies properties based on what changed, using cascading logic.
    /// </summary>
    public void NotifyPropertiesChanged(
        bool musicTypeChanged,
        bool languageCodeChanged,
        bool publicationCodeChanged,
        bool trackNumberChanged,
        bool repeatChanged,
        MusicType? musicType,
        Action<bool>? setShouldScrollToBottom = null)
    {
        // Determine which properties need to be notified (cascading logic)
        var notifyMusicType = musicTypeChanged;
        var notifyLanguage = musicTypeChanged || languageCodeChanged;
        var notifySongBook = musicTypeChanged || languageCodeChanged || publicationCodeChanged;
        var notifyTrack = musicTypeChanged || languageCodeChanged || publicationCodeChanged || trackNumberChanged;

        // Music type change cascades to all below
        if (notifyMusicType)
        {
            onPropertyChanged(nameof(MusicSelectionContainerViewModel.MusicTypeDisplayText));
            onPropertyChanged(nameof(MusicSelectionContainerViewModel.IsMusicLanguageVisible));
            onPropertyChanged(nameof(MusicSelectionContainerViewModel.IsSongBookVisible));

            // Clear cached values when music type changes
            displayTextProvider.ClearCaches();

            // For vocals: notify language, song book, and track
            // For melodies: only notify track
            if (musicType == MusicType.Vocals)
            {
                onPropertyChanged(nameof(MusicSelectionContainerViewModel.MusicLanguageDisplayText));
                onPropertyChanged(nameof(MusicSelectionContainerViewModel.SongBookDisplayText));

                // Signal to scroll to bottom when user selects vocals (only if music is enabled)
                setShouldScrollToBottom?.Invoke(true);
            }
            onPropertyChanged(nameof(MusicSelectionContainerViewModel.TrackDisplayText));
        }
        // Language change (vocals only) cascades to song book and track
        else if (notifyLanguage && musicType == MusicType.Vocals)
        {
            onPropertyChanged(nameof(MusicSelectionContainerViewModel.MusicLanguageDisplayText));
            onPropertyChanged(nameof(MusicSelectionContainerViewModel.SongBookDisplayText));
            onPropertyChanged(nameof(MusicSelectionContainerViewModel.TrackDisplayText));

            // Clear song book and track caches when language changes
            displayTextProvider.ClearSongBookCache();
            displayTextProvider.ClearTrackCache();
        }
        // Song book change cascades to track
        else if (notifySongBook)
        {
            onPropertyChanged(nameof(MusicSelectionContainerViewModel.SongBookDisplayText));
            onPropertyChanged(nameof(MusicSelectionContainerViewModel.TrackDisplayText));

            // Clear track cache when song book changes
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
        if (notifyMusicType || notifyLanguage || notifySongBook || notifyTrack || repeatChanged)
        {
            onPropertyChanged(nameof(MusicSelectionContainerViewModel.IsRepeatEnabled));
            onPropertyChanged(nameof(MusicSelectionContainerViewModel.HasTrackSelected));
        }
    }
}

