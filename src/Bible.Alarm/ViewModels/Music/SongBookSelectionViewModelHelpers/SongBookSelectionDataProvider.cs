#nullable enable
using System.Collections.ObjectModel;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.Shared;
using Serilog;

namespace Bible.Alarm.ViewModels.Music.SongBookSelectionViewModelHelpers;

/// <summary>
/// Handles data population for SongBookSelectionViewModel.
/// </summary>
public sealed class SongBookSelectionDataProvider(IMediaService mediaService)
{
    private readonly Dictionary<string, PublicationListViewItemModel> songBookVMsMapping = [];

    public Dictionary<string, PublicationListViewItemModel> SongBookVMsMapping => songBookVMsMapping;

    public async Task PopulateLanguages(
        AlarmMusic? current,
        ObservableCollection<LanguageListViewItemModel> languages,
        Action<LanguageListViewItemModel?> setCurrentLanguage,
        string? searchTerm = null)
    {
        // Do ALL processing on background thread to avoid blocking spinner animation
        var (languageVMs, selectedLanguage) = await Task.Run(async () =>
        {
            var languagesFromDb = await mediaService.GetVocalMusicLanguages();
            var trimmedSearchTerm = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm.Trim();
            
            var vms = new List<LanguageListViewItemModel>();
            LanguageListViewItemModel? selected = null;

            foreach (var language in languagesFromDb.Values
                         .Where(x => trimmedSearchTerm == null
                                     || x.Name.Contains(trimmedSearchTerm, StringComparison.OrdinalIgnoreCase))
                         .OrderBy(x => x.Name))
            {
                var languageVm = new LanguageListViewItemModel(language);
                vms.Add(languageVm);

                if (current != null && languageVm.Code == current.LanguageCode)
                {
                    languageVm.IsSelected = true;
                    selected = languageVm;
                }
            }

            return (vms, selected);
        });

        // Add items in small batches with frequent yields for smooth spinner animation
        const int batchSize = 15;
        await MainThread.InvokeOnMainThreadAsync(() => languages.Clear());
        await Task.Yield(); // Let spinner animate after clear
        
        for (int i = 0; i < languageVMs.Count; i += batchSize)
        {
            var batch = languageVMs.Skip(i).Take(batchSize).ToList();
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                foreach (var lang in batch)
                {
                    languages.Add(lang);
                }
            });
            
            // Yield after every batch for smooth animation
            await Task.Yield();
        }
        
        if (selectedLanguage != null)
        {
            await MainThread.InvokeOnMainThreadAsync(() => setCurrentLanguage(selectedLanguage));
        }
    }

    public async Task PopulateSongBooks(
        string languageCode,
        AlarmMusic? current,
        ObservableCollection<PublicationListViewItemModel> songBooks,
        Action<PublicationListViewItemModel?> setSelectedSongBook)
    {
        // Do ALL processing on background thread to avoid blocking spinner animation
        var (songBookVMs, newMapping, selectedSongBook) = await Task.Run(async () =>
        {
            var songBooksFromDb = await mediaService.GetVocalMusicReleases(languageCode);
            var vms = new List<PublicationListViewItemModel>();
            var mapping = new Dictionary<string, PublicationListViewItemModel>();
            PublicationListViewItemModel? selected = null;

            foreach (var release in songBooksFromDb.Values)
            {
                // Skip duplicates - if code already exists, use the existing one
                if (mapping.TryGetValue(release.Code, out var existingVm))
                {
                    // Still check if this duplicate matches the current publication code
                    if (current != null &&
                        current.MusicType == MusicType.Vocals &&
                        current.LanguageCode == languageCode &&
                        current.PublicationCode == release.Code)
                    {
                        existingVm.IsSelected = true;
                        selected = existingVm;
                    }
                    continue;
                }

                var songBookListViewItemModel = new PublicationListViewItemModel(release);
                vms.Add(songBookListViewItemModel);
                mapping[songBookListViewItemModel.Code] = songBookListViewItemModel;

                if (current != null &&
                    current.MusicType == MusicType.Vocals &&
                    current.LanguageCode == languageCode &&
                    current.PublicationCode == release.Code)
                {
                    songBookListViewItemModel.IsSelected = true;
                    selected = songBookListViewItemModel;
                }
            }

            return (vms, mapping, selected);
        });

        // Update mapping
        songBookVMsMapping.Clear();
        foreach (var kvp in newMapping)
        {
            songBookVMsMapping[kvp.Key] = kvp.Value;
        }

        // Minimal UI thread work - just swap the collection contents
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            songBooks.Clear();
            foreach (var songBook in songBookVMs)
            {
                songBooks.Add(songBook);
            }
            if (selectedSongBook != null)
            {
                setSelectedSongBook(selectedSongBook);
            }
        });
    }

    public void SetSelectedSongBook(
        AlarmMusic? current,
        Dictionary<string, PublicationListViewItemModel> songBookVMsMapping,
        PublicationListViewItemModel? currentSelectedSongBook,
        Action<PublicationListViewItemModel?> setSelectedSongBook)
    {
        if (current == null)
        {
            return;
        }

        if (currentSelectedSongBook != null)
        {
            currentSelectedSongBook.IsSelected = false;
        }

        if (!songBookVMsMapping.TryGetValue(current.PublicationCode, out var songBook))
        {
            return;
        }

        setSelectedSongBook(songBook);
        songBook.IsSelected = true;
    }

    public async Task<(int TrackNumber, string TrackName)> GetTrackForSongBookAsync(
        PublicationListViewItemModel songBook,
        string languageCode,
        ScheduleStateItem? currentSchedule)
    {
        var isSameSongBook = IsSameSongBook(currentSchedule, languageCode, songBook.Code);

        var tracks = await Task.Run(async () =>
            await mediaService.GetVocalMusicTracks(languageCode, songBook.Code));

        if (tracks == null || tracks.Count == 0)
        {
            return (0, string.Empty);
        }

        if (isSameSongBook &&
            currentSchedule?.MusicTrackNumber.HasValue == true &&
            tracks.TryGetValue(currentSchedule.MusicTrackNumber.Value, out var currentTrack))
        {
            return (currentSchedule.MusicTrackNumber.Value, currentTrack.Title);
        }

        var tracksList = tracks.Values.ToList();
        var randomTrack = tracksList[Random.Shared.Next(tracksList.Count)];
        return (randomTrack.Number, randomTrack.Title);
    }

    public async Task<(string? PublicationCode, int TrackNumber, string TrackName, string PublicationName)> GetFirstSongBookAndTrackForLanguageAsync(
        LanguageListViewItemModel language,
        ScheduleStateItem? currentSchedule)
    {
        var songBooks = await Task.Run(async () =>
            await mediaService.GetVocalMusicReleases(language.Code));

        if (songBooks == null || songBooks.Count == 0)
        {
            return (null, 0, string.Empty, string.Empty);
        }

        var firstSongBook = songBooks.FirstOrDefault();
        if (firstSongBook.Value == null)
        {
            return (null, 0, string.Empty, string.Empty);
        }

        var publicationCode = firstSongBook.Key;
        var isSameLanguage = IsSameLanguageAndSongBook(currentSchedule, language.Code, publicationCode);

        var tracks = await Task.Run(async () =>
            await mediaService.GetVocalMusicTracks(language.Code, publicationCode));

        if (tracks == null || tracks.Count == 0)
        {
            return (null, 0, string.Empty, string.Empty);
        }

        int trackNumber;
        string trackName;

        if (isSameLanguage &&
            currentSchedule?.MusicTrackNumber.HasValue == true &&
            tracks.TryGetValue(currentSchedule.MusicTrackNumber.Value, out var currentTrack))
        {
            trackNumber = currentSchedule.MusicTrackNumber.Value;
            trackName = currentTrack.Title;
        }
        else
        {
            var tracksList = tracks.Values.ToList();
            var randomTrack = tracksList[Random.Shared.Next(tracksList.Count)];
            trackNumber = randomTrack.Number;
            trackName = randomTrack.Title;
        }

        return (publicationCode, trackNumber, trackName, firstSongBook.Value.Name);
    }

    private static bool IsSameSongBook(ScheduleStateItem? currentSchedule, string languageCode, string publicationCode)
    {
        return currentSchedule != null &&
               currentSchedule.MusicType == MusicType.Vocals &&
               currentSchedule.MusicLanguageCode == languageCode &&
               currentSchedule.MusicPublicationCode == publicationCode;
    }

    private static bool IsSameLanguageAndSongBook(ScheduleStateItem? currentSchedule, string languageCode, string publicationCode)
    {
        return currentSchedule != null &&
               currentSchedule.MusicType == MusicType.Vocals &&
               currentSchedule.MusicLanguageCode == languageCode &&
               currentSchedule.MusicPublicationCode == publicationCode;
    }
}

