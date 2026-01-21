#nullable enable
using System.Collections.ObjectModel;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.Shared;

namespace Bible.Alarm.ViewModels.Music.SongPublicationSelectionViewModelHelpers;

/// <summary>
/// Handles data population for SongPublicationSelectionViewModel.
/// </summary>
public sealed class SongPublicationSelectionDataProvider(IMediaService mediaService)
{
    private readonly Dictionary<string, PublicationListViewItemModel> songPublicationVMsMapping = [];
    private readonly SemaphoreSlim languagePopulationLock = new(1, 1);

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
        });
    }

    public async Task PopulateSongPublications(
        string? languageCode,
        AlarmMusic? current,
        ObservableCollection<PublicationListViewItemModel> songPublications,
        Action<PublicationListViewItemModel?> setSelectedSongPublication)
    {
        // Do ALL processing on background thread to avoid blocking spinner animation
        var (songPublicationVMs, newMapping, selectedSongPublication) = await Task.Run(async () =>
        {
            Dictionary<string, VocalMusic>? vocalReleases = null;
            Dictionary<string, MelodyMusic>? melodyReleases = null;
            
            // Determine which type of music to load based on current music type
            if (current?.MusicType == MusicType.Music)
            {
                // Instrumental music - load melody releases (no language code needed)
                melodyReleases = await mediaService.GetMelodyMusicReleases();
            }
            else if (!string.IsNullOrEmpty(languageCode))
            {
                // Vocal music - load vocal releases with language code
                vocalReleases = await mediaService.GetVocalMusicReleases(languageCode);
            }
            else
            {
                // No music type or language code - return empty
                return (new List<PublicationListViewItemModel>(), new Dictionary<string, PublicationListViewItemModel>(), (PublicationListViewItemModel?)null);
            }

            var vms = new List<PublicationListViewItemModel>();
            var mapping = new Dictionary<string, PublicationListViewItemModel>();
            PublicationListViewItemModel? selected = null;

            // Process vocal music releases
            if (vocalReleases != null)
            {
                foreach (var release in vocalReleases.Values)
                {
                    // Skip duplicates - if code already exists, use the existing one
                    if (mapping.TryGetValue(release.Code, out var existingVm))
                    {
                        // Still check if this duplicate matches the current publication code
                        if (current != null &&
                            current.MusicType == MusicType.VocalMusic &&
                            current.LanguageCode == languageCode &&
                            current.PublicationCode == release.Code)
                        {
                            existingVm.IsSelected = true;
                            selected = existingVm;
                        }
                        continue;
                    }

                    var songPublicationListViewItemModel = new PublicationListViewItemModel(release);
                    vms.Add(songPublicationListViewItemModel);
                    mapping[songPublicationListViewItemModel.Code] = songPublicationListViewItemModel;

                    if (current != null &&
                        current.MusicType == MusicType.VocalMusic &&
                        current.LanguageCode == languageCode &&
                        current.PublicationCode == release.Code)
                    {
                        songPublicationListViewItemModel.IsSelected = true;
                        selected = songPublicationListViewItemModel;
                    }
                }
            }

            // Process melody music releases
            if (melodyReleases != null)
            {
                foreach (var release in melodyReleases.Values)
                {
                    var publicationCode = release.Publication.PublicationCode;
                    
                    // Skip duplicates - if code already exists, use the existing one
                    if (mapping.TryGetValue(publicationCode, out var existingVm))
                    {
                        // Still check if this duplicate matches the current publication code
                        if (current != null &&
                            current.MusicType == MusicType.Music &&
                            current.PublicationCode == publicationCode)
                        {
                            existingVm.IsSelected = true;
                            selected = existingVm;
                        }
                        continue;
                    }

                    // Use the Publication property directly (BiblePublication is a Publication)
                    var songPublicationListViewItemModel = new PublicationListViewItemModel(release.Publication);
                    vms.Add(songPublicationListViewItemModel);
                    mapping[songPublicationListViewItemModel.Code] = songPublicationListViewItemModel;

                    if (current != null &&
                        current.MusicType == MusicType.Music &&
                        current.PublicationCode == publicationCode)
                    {
                        songPublicationListViewItemModel.IsSelected = true;
                        selected = songPublicationListViewItemModel;
                    }
                }
            }

            return (vms, mapping, selected);
        });

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
        ScheduleStateItem? currentSchedule)
    {
        var isSameSongPublication = IsSameSongPublication(currentSchedule, languageCode, songPublication.Code);

        var tracks = await Task.Run(async () =>
            await mediaService.GetVocalMusicTracks(languageCode, songPublication.Code));

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
        ScheduleStateItem? currentSchedule)
    {
        var songPublications = await Task.Run(async () =>
            await mediaService.GetVocalMusicReleases(language.Code));

        if (songPublications == null || songPublications.Count == 0)
        {
            return (null, 0, string.Empty, string.Empty);
        }

        var firstSongPublication = songPublications.FirstOrDefault();
        if (firstSongPublication.Value == null)
        {
            return (null, 0, string.Empty, string.Empty);
        }

        var publicationCode = firstSongPublication.Key;
        var isSameLanguage = IsSameLanguageAndSongPublication(currentSchedule, language.Code, publicationCode);

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

        return (publicationCode, trackNumber, trackName, firstSongPublication.Value.Name);
    }

    private static bool IsSameSongPublication(ScheduleStateItem? currentSchedule, string languageCode, string publicationCode)
    {
        return currentSchedule != null &&
               currentSchedule.MusicType == MusicType.VocalMusic &&
               currentSchedule.MusicLanguageCode == languageCode &&
               currentSchedule.MusicPublicationCode == publicationCode;
    }

    private static bool IsSameLanguageAndSongPublication(ScheduleStateItem? currentSchedule, string languageCode, string publicationCode)
    {
        return currentSchedule != null &&
               currentSchedule.MusicType == MusicType.VocalMusic &&
               currentSchedule.MusicLanguageCode == languageCode &&
               currentSchedule.MusicPublicationCode == publicationCode;
    }
}

