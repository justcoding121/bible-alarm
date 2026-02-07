#nullable enable
using System.Collections.ObjectModel;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Bible.Alarm.ViewModels.Music.MusicPublicationSelectionViewModelHelpers;

/// <summary>
/// Handles data population for MusicPublicationSelectionViewModel.
/// </summary>
public sealed class MusicPublicationSelectionDataProvider(
    IMediaService mediaService,
    IBiblePublicationService? biblePublicationService = null,
    ILanguageContentService? languageContentService = null,
    IServiceScopeFactory? scopeFactory = null)
{
    private readonly Dictionary<string, PublicationListViewItemModel> songPublicationVMsMapping = [];
    private readonly SemaphoreSlim languagePopulationLock = new(1, 1);
    private readonly MusicPublicationFetchCoordinator fetchCoordinator = new(mediaService);
    private readonly VocalMusicFirstPublicationTrackSelector firstVocalSelector = new(mediaService, biblePublicationService, languageContentService, scopeFactory);

    public Dictionary<string, PublicationListViewItemModel> SongPublicationVMsMapping => songPublicationVMsMapping;

    public async Task PopulateLanguages(
        AlarmMusic? current,
        ObservableCollection<LanguageListViewItemModel> languages,
        Action<LanguageListViewItemModel?> setCurrentLanguage,
        string? searchTerm = null)
    {
        // Prevent concurrent population which can cause duplicates
        await ConcurrencyHelper.ExecuteAsync(languagePopulationLock, async () =>
        {
            // Do ALL processing on background thread to avoid blocking spinner animation
            // Use GetBiblePublicationLanguages with category="Music" (same API as Bible publication)
            var languagesFromDb = await mediaService.GetBiblePublicationLanguages("Music");
            var trimmedSearchTerm = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm.Trim();

            var languageVMs = new List<LanguageListViewItemModel>();
            LanguageListViewItemModel? selectedLanguage = null;

            foreach (var language in languagesFromDb.Values
                         .Where(x => trimmedSearchTerm == null
                                     || x.Name.Contains(trimmedSearchTerm, StringComparison.OrdinalIgnoreCase))
                         .OrderBy(x => x.Name))
            {
                var languageVm = new LanguageListViewItemModel(language);
                languageVMs.Add(languageVm);

                if (current != null && 
                    string.Equals(languageVm.Code, current.LanguageCode, StringComparison.OrdinalIgnoreCase))
                {
                    languageVm.IsSelected = true;
                    selectedLanguage = languageVm;
                    Log.Debug("PopulateLanguages: Marked language {LanguageCode} ({LanguageName}) as selected",
                        languageVm.Code, languageVm.Name);
                }
            }

            // Add items in small batches with frequent yields for smooth spinner animation
            const int batchSize = 15;
            await MainThread.InvokeOnMainThreadAsync(() => languages.Clear());
            // Let spinner animate after clear
            await Task.Yield();

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
        });
    }

    public async Task PopulateSongPublications(
        string? languageCode,
        AlarmMusic? current,
        ObservableCollection<PublicationListViewItemModel> songPublications,
        Action<PublicationListViewItemModel?> setSelectedSongPublication,
        bool downloadAll = false,
        IFetchProgress? progress = null,
        CancellationToken cancellationToken = default)
    {
        // Use GetBiblePublications with category="Music" - same API as Bible publication container
        // This returns both publications with language AND without language FK (data-driven)
        // downloadAll=true when publication modal opens (download all publications with first sections and tracks)
        // downloadAll=false when language changes (only download first publication in cascade)
        var publicationsData = await fetchCoordinator.FetchMusicPublicationsAsync(languageCode, current, downloadAll, progress, cancellationToken);

        var songPublicationVMs = new List<PublicationListViewItemModel>();
        var newMapping = new Dictionary<string, PublicationListViewItemModel>();
        PublicationListViewItemModel? selectedSongPublication = null;

        if (publicationsData != null && publicationsData.Count > 0)
        {
            // Show ALL music publications (both with and without LanguageId)
            // This matches Bible container behavior - merging languaged and non-languaged publications
            foreach (var publication in publicationsData.Values)
            {
                // Skip duplicates - if code already exists, use the existing one
                if (newMapping.TryGetValue(publication.PublicationCode, out var existingVm))
                {
                    // Check if this matches the current publication code
                    if (current != null && current.PublicationCode == publication.PublicationCode)
                    {
                        existingVm.IsSelected = true;
                        selectedSongPublication = existingVm;
                    }
                    continue;
                }

                var songPublicationListViewItemModel = new PublicationListViewItemModel(publication);
                songPublicationVMs.Add(songPublicationListViewItemModel);
                newMapping[songPublicationListViewItemModel.Code] = songPublicationListViewItemModel;

                // Check if this matches the current publication code
                if (current != null && current.PublicationCode == publication.PublicationCode)
                {
                    songPublicationListViewItemModel.IsSelected = true;
                    selectedSongPublication = songPublicationListViewItemModel;
                }
            }

            // Sort publications by category: Music = osg first, then others by name
            songPublicationVMs = PublicationSortHelper.SortByPriorityForCategory(songPublicationVMs, p => p.Code, p => p.Name, "Music").ToList();
        }

        // Update mapping
        songPublicationVMsMapping.Clear();
        foreach (var kvp in newMapping)
        {
            songPublicationVMsMapping[kvp.Key] = kvp.Value;
        }

        // Minimal UI thread work - just swap the collection contents
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            songPublications.Clear();
            foreach (var songPublication in songPublicationVMs)
            {
                songPublications.Add(songPublication);
            }
            if (selectedSongPublication != null)
            {
                setSelectedSongPublication(selectedSongPublication);
            }
        });
    }

    public void SetSelectedSongPublication(
        AlarmMusic? current,
        Dictionary<string, PublicationListViewItemModel> songPublicationVMsMapping,
        PublicationListViewItemModel? currentSelectedSongPublication,
        Action<PublicationListViewItemModel?> setSelectedSongPublication)
    {
        if (current == null)
        {
            return;
        }

        if (currentSelectedSongPublication != null)
        {
            currentSelectedSongPublication.IsSelected = false;
        }

        if (!songPublicationVMsMapping.TryGetValue(current.PublicationCode, out var songPublication))
        {
            return;
        }

        setSelectedSongPublication(songPublication);
        songPublication.IsSelected = true;
    }

    public async Task<(string TrackCode, string TrackName)> GetTrackForSongPublicationAsync(
        PublicationListViewItemModel songPublication,
        string languageCode,
        ScheduleStateItem? currentSchedule,
        IFetchProgress? progress = null)
    {
        var isSameSongPublication = IsSameSongPublication(currentSchedule, languageCode, songPublication.Code);

        var tracks = await mediaService.GetVocalMusicTracks(languageCode, songPublication.Code);

        if (tracks == null || tracks.Count == 0)
        {
            return (string.Empty, string.Empty);
        }

        if (isSameSongPublication &&
            !string.IsNullOrWhiteSpace(currentSchedule?.MusicTrackCode) &&
            int.TryParse(currentSchedule.MusicTrackCode, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsedTrackNum) &&
            tracks.TryGetValue(parsedTrackNum, out var currentTrack))
        {
            return (currentSchedule.MusicTrackCode, currentTrack.Title);
        }

        var tracksList = tracks.Values.ToList();
        var randomTrack = tracksList[Random.Shared.Next(tracksList.Count)];
        return (randomTrack.Number.ToString(System.Globalization.CultureInfo.InvariantCulture), randomTrack.Title);
    }

    public async Task<(string? PublicationCode, string TrackCode, string TrackName, string PublicationName)> GetFirstSongPublicationAndTrackForLanguageAsync(
        LanguageListViewItemModel language,
        ScheduleStateItem? currentSchedule,
        IFetchProgress? progress = null)
    {
        return await firstVocalSelector.GetFirstSongPublicationAndTrackForLanguageAsync(language, currentSchedule, progress);
    }

    private static bool IsSameSongPublication(ScheduleStateItem? currentSchedule, string languageCode, string publicationCode)
    {
        return currentSchedule != null &&
               currentSchedule.MusicLanguageCode == languageCode &&
               currentSchedule.MusicPublicationCode == publicationCode;
    }
}
