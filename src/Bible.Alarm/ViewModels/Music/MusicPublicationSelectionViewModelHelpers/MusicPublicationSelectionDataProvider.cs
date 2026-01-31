#nullable enable
using System.Collections.ObjectModel;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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

                if (current != null && languageVm.Code == current.LanguageCode)
                {
                    languageVm.IsSelected = true;
                    selectedLanguage = languageVm;
                }
            }

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
        });
    }

    public async Task PopulateSongPublications(
        string? languageCode,
        AlarmMusic? current,
        ObservableCollection<PublicationListViewItemModel> songPublications,
        Action<PublicationListViewItemModel?> setSelectedSongPublication,
        bool downloadAll = false,
        IFetchProgress? progress = null)
    {
        // Use GetBiblePublications with category="Music" - same API as Bible publication container
        // This returns both publications with language AND without language FK (data-driven)
        // downloadAll=true when publication modal opens (download all publications with first sections and tracks)
        // downloadAll=false when language changes (only download first publication in cascade)
        var publicationsData = await fetchCoordinator.FetchMusicPublicationsAsync(languageCode, current, downloadAll, progress);

        var songPublicationVMs = new List<PublicationListViewItemModel>();
        var newMapping = new Dictionary<string, PublicationListViewItemModel>();
        PublicationListViewItemModel? selectedSongPublication = null;

        if (publicationsData != null && publicationsData.Count > 0)
        {
            // Process publications - filter based on MusicType
            foreach (var publication in publicationsData.Values)
            {
                // Filter based on MusicType:
                // - VocalMusic: needs publications with LanguageId (or both if language is selected)
                // - Music: needs publications without LanguageId
                if (current?.MusicType == MusicType.Music)
                {
                    // Instrumental music - only publications without LanguageId
                    if (publication.LanguageId != null)
                    {
                        continue;
                    }
                }
                else if (current?.MusicType == MusicType.VocalMusic)
                {
                    // Vocal music - only publications with LanguageId (when language is selected)
                    if (!string.IsNullOrEmpty(languageCode) && publication.LanguageId == null)
                    {
                        // Skip publications without language when language is selected for vocal music
                        // But keep them if they're already in the list (from previous selection)
                        continue;
                    }
                }

                // Skip duplicates - if code already exists, use the existing one
                if (newMapping.TryGetValue(publication.PublicationCode, out var existingVm))
                {
                    // Check if this matches the current publication code
                    if (current != null && current.PublicationCode == publication.PublicationCode)
                    {
                        var isVocalMatch = current.MusicType == MusicType.VocalMusic &&
                                           current.LanguageCode == languageCode &&
                                           publication.LanguageId != null;
                        var isInstrumentalMatch = current.MusicType == MusicType.Music &&
                                                  publication.LanguageId == null;

                        if (isVocalMatch || isInstrumentalMatch)
                        {
                            existingVm.IsSelected = true;
                            selectedSongPublication = existingVm;
                        }
                    }
                    continue;
                }

                var songPublicationListViewItemModel = new PublicationListViewItemModel(publication);
                songPublicationVMs.Add(songPublicationListViewItemModel);
                newMapping[songPublicationListViewItemModel.Code] = songPublicationListViewItemModel;

                // Check if this matches the current publication code
                if (current != null && current.PublicationCode == publication.PublicationCode)
                {
                    var isVocalMatch = current.MusicType == MusicType.VocalMusic &&
                                       current.LanguageCode == languageCode &&
                                       publication.LanguageId != null;
                    var isInstrumentalMatch = current.MusicType == MusicType.Music &&
                                              publication.LanguageId == null;

                    if (isVocalMatch || isInstrumentalMatch)
                    {
                        songPublicationListViewItemModel.IsSelected = true;
                        selectedSongPublication = songPublicationListViewItemModel;
                    }
                }
            }

            // Sort publications: nwt first, then bi12, then others by name (same as Bible publication)
            songPublicationVMs = PublicationSortHelper.SortByPriority(songPublicationVMs, p => p.Code, p => p.Name).ToList();
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

    public async Task<(int TrackNumber, string TrackName)> GetTrackForSongPublicationAsync(
        PublicationListViewItemModel songPublication,
        string languageCode,
        ScheduleStateItem? currentSchedule,
        IFetchProgress? progress = null)
    {
        var isSameSongPublication = IsSameSongPublication(currentSchedule, languageCode, songPublication.Code);

        var tracks = await mediaService.GetVocalMusicTracks(languageCode, songPublication.Code);

        if (tracks == null || tracks.Count == 0)
        {
            return (0, string.Empty);
        }

        if (isSameSongPublication &&
            currentSchedule?.MusicTrackNumber.HasValue == true &&
            tracks.TryGetValue(currentSchedule.MusicTrackNumber.Value, out var currentTrack))
        {
            return (currentSchedule.MusicTrackNumber.Value, currentTrack.Title);
        }

        var tracksList = tracks.Values.ToList();
        var randomTrack = tracksList[Random.Shared.Next(tracksList.Count)];
        return (randomTrack.Number, randomTrack.Title);
    }

    public async Task<(string? PublicationCode, int TrackNumber, string TrackName, string PublicationName)> GetFirstSongPublicationAndTrackForLanguageAsync(
        LanguageListViewItemModel language,
        ScheduleStateItem? currentSchedule,
        IFetchProgress? progress = null)
    {
        return await firstVocalSelector.GetFirstSongPublicationAndTrackForLanguageAsync(language, currentSchedule, progress);
    }

    private static bool IsSameSongPublication(ScheduleStateItem? currentSchedule, string languageCode, string publicationCode)
    {
        return currentSchedule != null &&
               currentSchedule.MusicType == MusicType.VocalMusic &&
               currentSchedule.MusicLanguageCode == languageCode &&
               currentSchedule.MusicPublicationCode == publicationCode;
    }

    // IsSameLanguageAndSongPublication moved to VocalMusicFirstPublicationTrackSelector
}

