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
public sealed class SongBookSelectionDataProvider(ILogger logger, IMediaService mediaService)
{
    private readonly Dictionary<string, PublicationListViewItemModel> songBookVMsMapping = [];

    public Dictionary<string, PublicationListViewItemModel> SongBookVMsMapping => songBookVMsMapping;

    public async Task PopulateLanguages(
        AlarmMusic? current,
        ObservableCollection<LanguageListViewItemModel> languages,
        Action<LanguageListViewItemModel?> setCurrentLanguage,
        string? searchTerm = null)
    {
        // Run database operations off UI thread
        var languagesFromDb = await Task.Run(async () =>
            await mediaService.GetVocalMusicLanguages());
        var languageVMs = new ObservableCollection<LanguageListViewItemModel>();

        // Trim the search term before using it
        var trimmedSearchTerm = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm.Trim();

        foreach (var language in languagesFromDb.Select(x => x.Value)
                     .Where(x => trimmedSearchTerm == null
                                 || x.Name.Contains(trimmedSearchTerm, StringComparison.OrdinalIgnoreCase))
                     .OrderBy(x => x.Name))
        {
            var languageVm = new LanguageListViewItemModel(language);

            languageVMs.Add(languageVm);

            if (current == null || languageVm.Code != current.LanguageCode)
            {
                continue;
            }

            languageVm.IsSelected = true;
            setCurrentLanguage(languageVm);
        }

        // Assign collection on main thread to ensure UI updates
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            languages.Clear();
            foreach (var lang in languageVMs)
            {
                languages.Add(lang);
            }
        });
    }

    public async Task PopulateSongBooks(
        string languageCode,
        AlarmMusic? current,
        ObservableCollection<PublicationListViewItemModel> songBooks,
        Action<PublicationListViewItemModel?> setSelectedSongBook)
    {
        songBookVMsMapping.Clear();

        // Run database operations off UI thread
        var songBooksFromDb = await Task.Run(async () =>
            await mediaService.GetVocalMusicReleases(languageCode));
        var songBookVMs = new ObservableCollection<PublicationListViewItemModel>();

        foreach (var release in songBooksFromDb.Select(x => x.Value))
        {
            var songBookListViewItemModel = new PublicationListViewItemModel(release);

            songBookVMs.Add(songBookListViewItemModel);
            songBookVMsMapping.Add(songBookListViewItemModel.Code, songBookListViewItemModel);

            if (current == null
                || current.MusicType != MusicType.Vocals
                || current.LanguageCode != languageCode
                || current.PublicationCode != release.Code)
            {
                continue;
            }

            songBookListViewItemModel.IsSelected = true;
            setSelectedSongBook(songBookListViewItemModel);
        }

        // Assign collection on main thread to ensure UI updates
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            songBooks.Clear();
            foreach (var songBook in songBookVMs)
            {
                songBooks.Add(songBook);
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

