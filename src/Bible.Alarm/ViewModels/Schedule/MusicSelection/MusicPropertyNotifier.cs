#nullable enable
using Bible.Alarm.Shared.Models.Enums;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Bible.Alarm.ViewModels.Schedule.MusicSelection;

/// <summary>
/// Handles cascading property change notifications for music properties.
/// Separated from MusicSelectionContainerViewModel for better modularity.
/// </summary>
public sealed class MusicPropertyNotifier
{
    private readonly ObservableObject viewModel;
    private readonly MusicDisplayTextProvider displayTextProvider;

    public MusicPropertyNotifier(ObservableObject viewModel, MusicDisplayTextProvider displayTextProvider)
    {
        this.viewModel = viewModel;
        this.displayTextProvider = displayTextProvider;
    }

    /// <summary>
    /// Notifies all music-related properties changed in a single batch.
    /// This reduces UI thread work compared to individual notifications.
    /// </summary>
    public void NotifyAllMusicPropertiesChanged()
    {
        viewModel.OnPropertyChanged(nameof(MusicSelectionContainerViewModel.MusicEnabled));
        viewModel.OnPropertyChanged(nameof(MusicSelectionContainerViewModel.MusicTypeDisplayText));
        viewModel.OnPropertyChanged(nameof(MusicSelectionContainerViewModel.IsMusicLanguageVisible));
        viewModel.OnPropertyChanged(nameof(MusicSelectionContainerViewModel.MusicLanguageDisplayText));
        viewModel.OnPropertyChanged(nameof(MusicSelectionContainerViewModel.IsSongBookVisible));
        viewModel.OnPropertyChanged(nameof(MusicSelectionContainerViewModel.SongBookDisplayText));
        viewModel.OnPropertyChanged(nameof(MusicSelectionContainerViewModel.TrackDisplayText));
        viewModel.OnPropertyChanged(nameof(MusicSelectionContainerViewModel.IsRepeatEnabled));
        viewModel.OnPropertyChanged(nameof(MusicSelectionContainerViewModel.HasTrackSelected));
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
            viewModel.OnPropertyChanged(nameof(MusicSelectionContainerViewModel.MusicTypeDisplayText));
            viewModel.OnPropertyChanged(nameof(MusicSelectionContainerViewModel.IsMusicLanguageVisible));
            viewModel.OnPropertyChanged(nameof(MusicSelectionContainerViewModel.IsSongBookVisible));

            // Clear cached values when music type changes
            displayTextProvider.ClearCaches();

            // For vocals: notify language, song book, and track
            // For melodies: only notify track
            if (musicType == MusicType.Vocals)
            {
                viewModel.OnPropertyChanged(nameof(MusicSelectionContainerViewModel.MusicLanguageDisplayText));
                viewModel.OnPropertyChanged(nameof(MusicSelectionContainerViewModel.SongBookDisplayText));

                // Signal to scroll to bottom when user selects vocals (only if music is enabled)
                setShouldScrollToBottom?.Invoke(true);
            }
            viewModel.OnPropertyChanged(nameof(MusicSelectionContainerViewModel.TrackDisplayText));
        }
        // Language change (vocals only) cascades to song book and track
        else if (notifyLanguage && musicType == MusicType.Vocals)
        {
            viewModel.OnPropertyChanged(nameof(MusicSelectionContainerViewModel.MusicLanguageDisplayText));
            viewModel.OnPropertyChanged(nameof(MusicSelectionContainerViewModel.SongBookDisplayText));
            viewModel.OnPropertyChanged(nameof(MusicSelectionContainerViewModel.TrackDisplayText));

            // Clear song book and track caches when language changes
            displayTextProvider.ClearSongBookCache();
            displayTextProvider.ClearTrackCache();
        }
        // Song book change cascades to track
        else if (notifySongBook)
        {
            viewModel.OnPropertyChanged(nameof(MusicSelectionContainerViewModel.SongBookDisplayText));
            viewModel.OnPropertyChanged(nameof(MusicSelectionContainerViewModel.TrackDisplayText));

            // Clear track cache when song book changes
            displayTextProvider.ClearTrackCache();
        }
        // Track change only affects itself
        else if (notifyTrack)
        {
            viewModel.OnPropertyChanged(nameof(MusicSelectionContainerViewModel.TrackDisplayText));

            // Clear track cache when track changes
            displayTextProvider.ClearTrackCache();
        }

        // Always notify repeat and has track selected if any music property changed
        if (notifyMusicType || notifyLanguage || notifySongBook || notifyTrack || repeatChanged)
        {
            viewModel.OnPropertyChanged(nameof(MusicSelectionContainerViewModel.IsRepeatEnabled));
            viewModel.OnPropertyChanged(nameof(MusicSelectionContainerViewModel.HasTrackSelected));
        }
    }
}

