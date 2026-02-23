#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.AudioLinksHarvestor.Models;
using Bible.Alarm.AudioLinksHarvestor.Utility;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Serilog;
using DirectoryHelper = Bible.Alarm.AudioLinksHarvestor.Utility.DirectoryHelper;

namespace Bible.Alarm.AudioLinksHarvestor.Harvestors;

internal class DramaHarvester : BaseHarvester
{
    private const int MaxConcurrentSectionDownloads = 8;
    private readonly IDataPersister? dataPersister;
    private readonly SignLanguageChecker signLanguageChecker;

    public DramaHarvester(ILogger logger, DownloadUtility downloadUtility, IDataPersister? dataPersister = null)
        : base(logger, downloadUtility)
    {
        this.dataPersister = dataPersister;
        signLanguageChecker = new SignLanguageChecker(logger, downloadUtility);
    }

    /// <summary>
    /// Localized category names: (languageCode, categoryKey) -> localizedName (from API response per language).
    /// </summary>
    private readonly ConcurrentDictionary<(string LanguageCode, string CategoryKey), string> localizedCategoryNames = new();

    internal async Task HarvestDramaLinks(bool isTestRun = false, IReadOnlySet<string>? publicationFilter = null)
    {
        // Track publications per language: languageCode -> set of publication codes
        var languageCodeToPublications = new ConcurrentDictionary<string, ConcurrentDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        // Harvest each Mediator API publication (dramas, series, children, broadcasting, family, etc.).
        var mediatorCodes = JwSourceHelper.AllMediatorPublicationCodes;
        var codesToHarvest = publicationFilter != null
            ? mediatorCodes.Where(c => publicationFilter.Contains(c)).ToList()
            : mediatorCodes.ToList();
        foreach (var publicationCode in codesToHarvest)
        {
            Logger.Information("Harvesting Drama publication: {PublicationCode}", publicationCode);

            await HarvestDramaPublication(
                publicationCode,
                publicationCode,
                languageCodeToPublications,
                isTestRun);
        }

    }

    private async Task HarvestDramaPublication(
        string publicationCode,
        string publicationName,
        ConcurrentDictionary<string, ConcurrentDictionary<string, string>> languageCodeToPublications,
        bool isTestRun)
    {
        var pathAndQuery = $"/categories/E/{publicationCode}?detailed=1";
        string? jsonString;
        try
        {
            jsonString = await DownloadUtility.GetMediatorAsync(pathAndQuery);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to fetch publication {PublicationCode} for English. Skipping.", publicationCode);
            return;
        }

        if (string.IsNullOrEmpty(jsonString))
        {
            Logger.Error("Empty response for publication {PublicationCode} for English. Skipping.", publicationCode);
            return;
        }

        // Parse the category response to get all unique languages
        var languagesFromCategory = DramaCategoryLanguageExtractor.ExtractLanguagesFromCategory(jsonString, Logger);
        if (languagesFromCategory.Count == 0)
        {
            Logger.Warning("No languages found for publication {PublicationCode}. Skipping.", publicationCode);
            return;
        }

        // Filter out sign languages
        languagesFromCategory = await signLanguageChecker.FilterSignLanguagesAsync(languagesFromCategory);

        if (languagesFromCategory.Count == 0)
        {
            Logger.Warning("No non-sign languages found for publication {PublicationCode}. Skipping.", publicationCode);
            return;
        }

        // Extract language info from English category response
        var discoveredLanguages = DramaCategoryLanguageExtractor.ExtractLanguageInfoFromCategory(jsonString, languagesFromCategory, Logger);

        // Filter out sign languages from discovered languages
        discoveredLanguages = await signLanguageChecker.FilterSignLanguagesAsync(discoveredLanguages);

        // Save discovered languages for on-demand fetching (excluding English)
        // The category API already lists only available languages, so no verification needed
        if (dataPersister != null && discoveredLanguages.Count > 0)
        {
            // Remove English from discovered languages since we're processing it
            var languagesToSave = discoveredLanguages
                .Where(kvp => !kvp.Key.Equals("E", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            
            if (languagesToSave.Count > 0)
            {
                await dataPersister.SavePublicationLanguages(publicationCode, languagesToSave);
            }
        }

        // Verify English (E) is available (it will be seeded separately after discovery)
        if (!languagesFromCategory.Contains("E", StringComparer.OrdinalIgnoreCase))
        {
            Logger.Warning("English (E) not found in discovered languages for publication {PublicationCode}. Skipping.", publicationCode);
            return;
        }

        Logger.Information("English (E) found for publication {PublicationName} - will be seeded separately", publicationName);
    }

    private async Task ProcessPublicationForLanguage(
        string publicationCode,
        string publicationName,
        string languageCode,
        ConcurrentDictionary<string, ConcurrentDictionary<string, string>> languageCodeToPublications)
    {
        Logger.Information("Processing {PublicationName} for language: {LanguageCode}", publicationName, languageCode);
        
        // Normalize language code to uppercase for consistent storage and comparison
        var normalizedLanguageCode = languageCode.ToUpperInvariant();
        Logger.Information("Harvesting {PublicationName} for language {LanguageCode}", publicationName, normalizedLanguageCode);

        var pathAndQuery = $"/categories/{normalizedLanguageCode}/{publicationCode}?detailed=1";
        string? jsonString;
        try
        {
            jsonString = await DownloadUtility.GetMediatorAsync(pathAndQuery);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("404") || ex.Message.Contains("Response status code"))
        {
            Logger.Warning("Publication {PublicationCode} not available for language {LanguageCode}. Skipping.", publicationCode, normalizedLanguageCode);
            return;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to fetch publication {PublicationCode} for language {LanguageCode}. Skipping.", publicationCode, normalizedLanguageCode);
            return;
        }

        if (string.IsNullOrEmpty(jsonString))
        {
            Logger.Warning("Empty response for publication {PublicationCode} in language {LanguageCode}. Skipping.", publicationCode, normalizedLanguageCode);
            return;
        }

        var (mediaItems, localizedPublicationName) = DramaSectionCodeExtractor.ExtractMediaItemsFromCategory(
            jsonString,
            publicationCode,
            normalizedLanguageCode,
            Logger);
        if (mediaItems.Count == 0)
        {
            Logger.Warning("No media items found for publication {PublicationCode} in language {LanguageCode}. Skipping.", publicationCode, normalizedLanguageCode);
            return;
        }

        if (!string.IsNullOrEmpty(localizedPublicationName))
        {
            localizedCategoryNames[(normalizedLanguageCode, publicationCode)] = localizedPublicationName;
        }

        var publicationsForLanguage = languageCodeToPublications.GetOrAdd(normalizedLanguageCode, _ => new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        var finalPublicationName = localizedPublicationName ?? publicationName;
        publicationsForLanguage[publicationCode] = finalPublicationName;

        var allTracks = new List<DramaTrack>();
        using var semaphore = new SemaphoreSlim(MaxConcurrentSectionDownloads, MaxConcurrentSectionDownloads);
        var mediaTasks = mediaItems.Select(async item =>
        {
            await semaphore.WaitAsync();
            try
            {
                var (tracks, _) = await HarvestSingleTrack(item.SectionCode, item.TrackNumber, normalizedLanguageCode, publicationCode);
                if (tracks != null && tracks.Count > 0)
                {
                    lock (allTracks)
                    {
                        allTracks.AddRange(tracks);
                    }
                }
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(mediaTasks);

        if (allTracks.Count == 0)
        {
            Logger.Warning("No tracks found for publication {PublicationCode} ({LanguageCode}). Skipping.", publicationCode, normalizedLanguageCode);
            return;
        }

        var tracksBySection = new Dictionary<string, List<DramaTrack>>(StringComparer.OrdinalIgnoreCase)
        {
            [publicationCode] = allTracks
        };
        var sectionNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (dataPersister != null)
        {
            await dataPersister.SaveDramaPublication(normalizedLanguageCode, publicationCode, finalPublicationName, tracksBySection, sectionNames);
        }
        else
        {
            DramaFilePersistence.SaveDramaSectionsAndTracks(publicationCode, normalizedLanguageCode, tracksBySection, sectionNames);
        }

        Logger.Information("Saved {Count} tracks for publication {PublicationCode} ({LanguageCode})", allTracks.Count, publicationCode, normalizedLanguageCode);
    }

    private async Task<(List<DramaTrack>? Tracks, string? SectionName)> HarvestSingleTrack(string sectionCode, int trackNumber, string languageCode, string publicationCode)
    {
        try
        {
            var isVideo = PublicationTypeHelper.IsVideo(publicationCode);
            var fileFormat = isVideo ? "MP4" : "MP3";
            var useDocidParam = sectionCode.StartsWith("docid:", StringComparison.OrdinalIgnoreCase);
            var useIssueParam = JwSourceHelper.SectionCodesUsingIssueParameter.Contains(sectionCode);
            var singleTrackNoParam = JwSourceHelper.SectionCodesSingleTrackNoParam.Contains(sectionCode);
            string harvestLink;
            if (useDocidParam)
            {
                var docidValue = sectionCode.Substring(6);
                harvestLink = $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?output=json&docid={docidValue}&track={trackNumber}&fileformat={fileFormat}&alllangs=0&langwritten={languageCode}";
            }
            else if (singleTrackNoParam)
            {
                harvestLink = $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?output=json&pub={sectionCode}&fileformat={fileFormat}&alllangs=0&langwritten={languageCode}";
            }
            else
            {
                harvestLink = $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?output=json&pub={sectionCode}&{(useIssueParam ? "issue" : "track")}={trackNumber}&fileformat={fileFormat}&alllangs=0&langwritten={languageCode}";
            }

            var jsonString = await DownloadUtility.GetAsync(harvestLink);

            var trackNumberForParser = (useDocidParam || !singleTrackNoParam) ? (int?)trackNumber : null;
            return DramaTrackParser.ParseTracksFromGetPubMediaLinks(jsonString, sectionCode, languageCode, Logger, isVideo, trackNumberForParser, useIssueParam, useDocidParam);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("404") || ex.Message.Contains("Response status code"))
        {
            Logger.Warning("Track {SectionCode}-{TrackNumber} not available for language {LanguageCode}. Skipping.", sectionCode, trackNumber, languageCode);
            return (null, null);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to fetch track {SectionCode}-{TrackNumber} in language {LanguageCode}. Skipping.", sectionCode, trackNumber, languageCode);
            return (null, null);
        }
    }

}
