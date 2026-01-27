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

namespace Bible.Alarm.ViewModels.Music.SongPublicationSelectionViewModelHelpers;

/// <summary>
/// Handles data population for SongPublicationSelectionViewModel.
/// </summary>
public sealed class SongPublicationSelectionDataProvider(
    IMediaService mediaService,
    IBiblePublicationService? biblePublicationService = null,
    ILanguageContentService? languageContentService = null,
    IServiceScopeFactory? scopeFactory = null)
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
            // Use GetBiblePublicationLanguages with category="Music" (same API as Bible publication)
            var (languageVMs, selectedLanguage) = await Task.Run(async () =>
            {
                var languagesFromDb = await mediaService.GetBiblePublicationLanguages("Music");
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
        Action<PublicationListViewItemModel?> setSelectedSongPublication,
        bool downloadAll = false,
        IFetchProgress? progress = null)
    {
        // Do ALL processing on background thread to avoid blocking spinner animation
        var (songPublicationVMs, newMapping, selectedSongPublication) = await Task.Run(async () =>
        {
            // Use GetBiblePublications with category="Music" - same API as Bible publication container
            // This returns both publications with language AND without language FK (data-driven)
            // downloadAll=true when publication modal opens (download all publications with first sections and tracks)
            // downloadAll=false when language changes (only download first publication in cascade)
            Dictionary<string, Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublication>? publicationsData = null;
            
            // For instrumental music (MusicType.Music), we need publications without language
            // For vocal music (MusicType.VocalMusic), we need publications with language (or both)
            if (current?.MusicType == MusicType.Music && string.IsNullOrEmpty(languageCode))
            {
                // Instrumental music only - get publications without language FK
                // Use empty string as language code to get all Music category publications
                // GetBiblePublications will return publications with LanguageId == null for Music category
                publicationsData = await mediaService.GetBiblePublications(string.Empty, "Music", downloadAll, progress);
                
                // Filter to only publications without LanguageId
                if (publicationsData != null)
                {
                    publicationsData = publicationsData
                        .Where(kvp => kvp.Value.LanguageId == null)
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                }
            }
            else if (!string.IsNullOrEmpty(languageCode))
            {
                // Language selected - GetBiblePublications returns BOTH:
                // 1. Publications with LanguageId != null (filtered by language code)
                // 2. Publications with LanguageId == null (no language FK)
                // This is data-driven and works for any category
                publicationsData = await mediaService.GetBiblePublications(languageCode, "Music", downloadAll, progress);
            }
            else
            {
                // No music type or language code - return empty
                return (new List<PublicationListViewItemModel>(), new Dictionary<string, PublicationListViewItemModel>(), (PublicationListViewItemModel?)null);
            }

            // Retry logic: If downloadAll=true and non-English, retry fetching until all publications are harvested
            // For non-English languages, publications need to be fetched, so we retry with increasing delays
            if (downloadAll && !string.IsNullOrEmpty(languageCode) && !languageCode.Equals("E", StringComparison.OrdinalIgnoreCase))
            {
                const int maxRetries = 10; // Up to 10 retries
                var retryDelay = 1000; // Start with 1 second
                var maxWaitTime = TimeSpan.FromSeconds(60); // Total max wait time of 60 seconds
                var startTime = DateTime.UtcNow;
                var allHarvested = false;
                var attempt = 0;
                
                Serilog.Log.Information("PopulateSongPublications: Starting fetch with retries for language={LanguageCode}, category=Music",
                    languageCode);
                
                while (!allHarvested && attempt < maxRetries && (DateTime.UtcNow - startTime) < maxWaitTime)
                {
                    attempt++;
                    
                    try
                    {
                        // Fetch publications (this triggers harvesting if needed)
                        // Note: This retry block only runs when languageCode is not empty (vocal music)
                        publicationsData = await mediaService.GetBiblePublications(languageCode, "Music", downloadAll, progress);
                        
                        // Wait a bit for background harvesting to start
                        await Task.Delay(500);
                        
                        // Re-query to check if publications are now harvested
                        var reQueriedData = await mediaService.GetBiblePublications(languageCode, "Music", downloadAll: false, progress);
                        
                        // Check if ALL publications are harvested (not placeholders)
                        // A publication is harvested if it has a name that's different from its code and has an ID > 0
                        var hasPublications = reQueriedData != null && reQueriedData.Values.Count > 0;
                        var allHarvestedCheck = hasPublications && reQueriedData!.Values.All(p => 
                            !string.IsNullOrEmpty(p.Name) && 
                            p.Name != p.PublicationCode && 
                            p.Id > 0);
                        
                        if (allHarvestedCheck)
                        {
                            publicationsData = reQueriedData;
                            allHarvested = true;
                            Serilog.Log.Information("PopulateSongPublications: All {Count} publications harvested on attempt {Attempt} for language={LanguageCode}",
                                publicationsData.Count, attempt, languageCode);
                        }
                        else
                        {
                            // Log which publications are still placeholders for debugging
                            if (reQueriedData != null)
                            {
                                var placeholders = reQueriedData.Values.Where(p => 
                                    string.IsNullOrEmpty(p.Name) || 
                                    p.Name == p.PublicationCode || 
                                    p.Id == 0).Select(p => p.PublicationCode).ToList();
                                
                                if (placeholders.Count > 0)
                                {
                                    Serilog.Log.Debug("PopulateSongPublications: Attempt {Attempt}: Still waiting for {Count} publications to be harvested: {Placeholders}",
                                        attempt, placeholders.Count, string.Join(", ", placeholders));
                                }
                                else if (!hasPublications)
                                {
                                    Serilog.Log.Debug("PopulateSongPublications: Attempt {Attempt}: No publications found yet, will retry",
                                        attempt);
                                }
                            }
                            
                            // Wait with increasing delay before retrying (1s, 2s, 3s, etc., up to 5s)
                            var delay = Math.Min(retryDelay * attempt, 5000);
                            await Task.Delay(delay);
                        }
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Warning(ex, "PopulateSongPublications: Attempt {Attempt} failed for language={LanguageCode}, will retry",
                            attempt, languageCode);
                        
                        // Wait before retrying on exception
                        var delay = Math.Min(retryDelay * attempt, 5000);
                        await Task.Delay(delay);
                    }
                }
                
                if (!allHarvested)
                {
                    Serilog.Log.Warning("PopulateSongPublications: Timeout after {Attempts} attempts waiting for all publications to be harvested for language {LanguageCode}. Some may still be placeholders.",
                        attempt, languageCode);
                    
                    // Use the last fetched data even if not all are harvested
                    if (publicationsData == null || publicationsData.Count == 0)
                    {
                        // Final attempt to get at least some data
                        try
                        {
                            publicationsData = await mediaService.GetBiblePublications(languageCode, "Music", downloadAll: false, progress);
                        }
                        catch (Exception ex)
                        {
                            Serilog.Log.Error(ex, "PopulateSongPublications: Final fetch attempt failed for language={LanguageCode}",
                                languageCode);
                        }
                    }
                }
            }
            else if (publicationsData == null)
            {
                // For English or when downloadAll=false, just fetch once
                if (current?.MusicType == MusicType.Music && string.IsNullOrEmpty(languageCode))
                {
                    var fetchedData = await mediaService.GetBiblePublications(string.Empty, "Music", downloadAll, progress);
                    if (fetchedData != null)
                    {
                        publicationsData = fetchedData
                            .Where(kvp => kvp.Value.LanguageId == null)
                            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                    }
                }
                else if (!string.IsNullOrEmpty(languageCode))
                {
                    publicationsData = await mediaService.GetBiblePublications(languageCode, "Music", downloadAll, progress);
                }
            }

            if (publicationsData == null || publicationsData.Count == 0)
            {
                return (new List<PublicationListViewItemModel>(), new Dictionary<string, PublicationListViewItemModel>(), (PublicationListViewItemModel?)null);
            }

            var vms = new List<PublicationListViewItemModel>();
            var mapping = new Dictionary<string, PublicationListViewItemModel>();
            PublicationListViewItemModel? selected = null;

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
                if (mapping.TryGetValue(publication.PublicationCode, out var existingVm))
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
                            selected = existingVm;
                        }
                    }
                    continue;
                }

                var songPublicationListViewItemModel = new PublicationListViewItemModel(publication);
                vms.Add(songPublicationListViewItemModel);
                mapping[songPublicationListViewItemModel.Code] = songPublicationListViewItemModel;

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
                        selected = songPublicationListViewItemModel;
                    }
                }
            }

            // Sort publications: nwt first, then bi12, then others by name (same as Bible publication)
            vms = PublicationSortHelper.SortByPriority(vms, p => p.Code, p => p.Name).ToList();

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
        ScheduleStateItem? currentSchedule,
        IFetchProgress? progress = null)
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
        ScheduleStateItem? currentSchedule,
        IFetchProgress? progress = null)
    {
        // Step 1: Get the first publication code by ID order from PublicationLanguages for Music category
        // This is the publication that should be downloaded when language is selected
        // Filter out publications without LanguageId (instrumental/melody music) - data-driven, not hard-coded
        string? firstPublicationCode = null;
        if (biblePublicationService != null)
        {
            var availablePublicationCodes = await Task.Run(async () =>
                await biblePublicationService.GetAvailablePublicationCodesAsync(language.Code, "Music"));
            
            // Get publications without LanguageId from BiblePublications (data-driven)
            if (scopeFactory == null)
            {
                Serilog.Log.Warning("GetFirstSongPublicationAndTrackForLanguageAsync: scopeFactory is null, cannot filter publications without LanguageId");
                return (null, 0, string.Empty, string.Empty);
            }
            
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
            
            var publicationsWithoutLanguage = await db.BiblePublications
                .AsNoTracking()
                .Where(bp => bp.Category != null && 
                            bp.Category.CategoryName == "Music" &&
                            bp.LanguageId == null)
                .Select(bp => bp.PublicationCode)
                .Distinct()
                .ToListAsync();
            
            // Filter out publications without LanguageId
            var vocalPublicationCodes = availablePublicationCodes
                .Where(code => !publicationsWithoutLanguage.Contains(code, StringComparer.OrdinalIgnoreCase))
                .ToList();
            
            if (vocalPublicationCodes.Count > 0)
            {
                // Get first publication code by ID order from PublicationLanguages
                // Check if it has LanguageId, if not, get next one
                var publicationLanguages = await db.PublicationLanguages
                    .AsNoTracking()
                    .Include(pl => pl.Language)
                    .Include(pl => pl.Category)
                    .Where(pl => pl.Language != null && 
                               pl.Language.LanguageCode == language.Code.ToUpperInvariant() &&
                               pl.Category != null &&
                               pl.Category.CategoryName == "Music")
                    .OrderBy(pl => pl.Id)
                    .ToListAsync();
                
                // Find first publication that has LanguageId in BiblePublications
                foreach (var pl in publicationLanguages)
                {
                    var hasLanguageId = await db.BiblePublications
                        .AsNoTracking()
                        .AnyAsync(bp => bp.PublicationCode == pl.PublicationCode && 
                                       bp.LanguageId != null &&
                                       bp.Language != null &&
                                       bp.Language.LanguageCode == language.Code.ToUpperInvariant());
                    
                    if (hasLanguageId)
                    {
                        firstPublicationCode = pl.PublicationCode;
                        break;
                    }
                }
                
                // Fallback: use first from vocal list if no publication with LanguageId found
                if (string.IsNullOrEmpty(firstPublicationCode))
                {
                    firstPublicationCode = vocalPublicationCodes.FirstOrDefault();
                }
            }
        }

        if (string.IsNullOrEmpty(firstPublicationCode))
        {
            Serilog.Log.Warning("GetFirstSongPublicationAndTrackForLanguageAsync: No first publication found for language={LanguageCode}", language.Code);
            return (null, 0, string.Empty, string.Empty);
        }

        Serilog.Log.Debug("GetFirstSongPublicationAndTrackForLanguageAsync: First publication by ID order={PublicationCode} for language={LanguageCode}",
            firstPublicationCode, language.Code);

        progress?.UpdateProgress(0.3);

        // Step 2: Download the first publication with its first section (if sectioned) and tracks
        // This happens when language is selected (cascade)
        // EnsurePublicationExistsAsync will download the publication, its first section (by ID order from SectionLanguages), and tracks
        if (languageContentService != null && !language.Code.Equals("E", StringComparison.OrdinalIgnoreCase))
        {
            Serilog.Log.Information("Downloading first vocal music publication {PublicationCode} (by ID order) for language {LanguageCode} (cascade)",
                firstPublicationCode, language.Code);

            try
            {
                progress?.UpdateProgress(0.5);
                // EnsurePublicationExistsAsync downloads the publication with its first section (by ID order) and tracks
                var fetchSuccess = await languageContentService.EnsurePublicationExistsAsync(
                    firstPublicationCode, language.Code, default, progress);

                if (!fetchSuccess)
                {
                    Serilog.Log.Warning("Failed to download first vocal music publication {PublicationCode} for language {LanguageCode}",
                        firstPublicationCode, language.Code);
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "Error downloading first vocal music publication {PublicationCode} for language {LanguageCode}",
                    firstPublicationCode, language.Code);
            }
        }

        // Step 3: Get the downloaded publication using GetBiblePublications (same API as Bible publication)
        progress?.UpdateProgress(0.7);
        var songPublications = await Task.Run(async () =>
            await mediaService.GetBiblePublications(language.Code, "Music", downloadAll: false, progress));

        if (songPublications == null || songPublications.Count == 0)
        {
            return (null, 0, string.Empty, string.Empty);
        }

        // Find first publication with LanguageId (vocal music)
        var firstSongPublication = songPublications.Values
            .Where(p => p.LanguageId != null)
            .OrderBy(p => p.Id)
            .FirstOrDefault();

        if (firstSongPublication == null)
        {
            return (null, 0, string.Empty, string.Empty);
        }

        var publicationCode = firstSongPublication.PublicationCode;
        var isSameLanguage = IsSameLanguageAndSongPublication(currentSchedule, language.Code, publicationCode);

        // Use GetBiblePublicationTracks for vocal music (same API as Bible publication)
        // For vocal music, we need to get tracks from the first section (or flat publication)
        progress?.UpdateProgress(0.8);
        var sections = await Task.Run(async () =>
            await mediaService.GetBiblePublicationSections(language.Code, publicationCode, progress));
        
        SortedDictionary<int, Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublicationTrack>? tracks = null;
        if (sections != null && sections.Count > 0)
        {
            // Sectioned publication - get tracks from first section
            var firstSection = sections.First();
            tracks = await Task.Run(async () =>
                await mediaService.GetBiblePublicationTracks(language.Code, publicationCode, firstSection.Key));
        }
        else
        {
            // Flat publication - get tracks directly (no sections)
            // For flat publications, GetBiblePublicationTracks with sectionNumber=0 should work
            tracks = await Task.Run(async () =>
                await mediaService.GetBiblePublicationTracks(language.Code, publicationCode, 0));
        }

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

        return (publicationCode, trackNumber, trackName, firstSongPublication.Name);
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

