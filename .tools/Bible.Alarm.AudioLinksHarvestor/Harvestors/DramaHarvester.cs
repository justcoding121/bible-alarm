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

    internal async Task HarvestDramaLinks(bool isTestRun = false)
    {
        // Track publications per language: languageCode -> set of publication codes
        var languageCodeToPublications = new ConcurrentDictionary<string, ConcurrentDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        // Harvest each drama, Series Mediator, and Children Mediator publication.
        // Publication name for each language comes from the category API response (category.name), not from code.
        foreach (var publicationCode in JwSourceHelper.DramaCategoryCodes
            .Union(JwSourceHelper.SeriesMediatorPublicationCodes)
            .Union(JwSourceHelper.ChildrenMediatorPublicationCodes))
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

        // Extract section codes from Mediator API response
        var (sectionCodes, localizedPublicationName) = DramaSectionCodeExtractor.ExtractSectionCodesFromCategory(
            jsonString,
            publicationCode,
            normalizedLanguageCode,
            Logger);
        if (sectionCodes.Count == 0)
        {
            Logger.Warning("No sections found for publication {PublicationCode} in language {LanguageCode}. Skipping.", publicationCode, normalizedLanguageCode);
            return;
        }

        // Store localized publication name if available
        if (!string.IsNullOrEmpty(localizedPublicationName))
        {
            localizedCategoryNames[(normalizedLanguageCode, publicationCode)] = localizedPublicationName;
        }

        // Get or create publications dictionary for this language
        var publicationsForLanguage = languageCodeToPublications.GetOrAdd(normalizedLanguageCode, _ => new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        // Track this publication for this language
        var finalPublicationName = localizedPublicationName ?? publicationName;
        publicationsForLanguage[publicationCode] = finalPublicationName;

        // Harvest tracks for each section using GETPUBMEDIALINKS
        var tracksBySection = new Dictionary<string, List<DramaTrack>>(StringComparer.OrdinalIgnoreCase);
        var sectionNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        
        using var semaphore = new SemaphoreSlim(MaxConcurrentSectionDownloads, MaxConcurrentSectionDownloads);
        var sectionTasks = sectionCodes.Select(async sectionCode =>
        {
            await semaphore.WaitAsync();
            try
            {
                var (tracks, sectionName) = await HarvestSectionTracks(sectionCode, normalizedLanguageCode, publicationCode);
                if (tracks != null && tracks.Count > 0)
                {
                    lock (tracksBySection)
                    {
                        tracksBySection[sectionCode] = tracks;
                        if (!string.IsNullOrEmpty(sectionName))
                        {
                            sectionNames[sectionCode] = sectionName;
                        }
                    }
                }
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(sectionTasks);

        if (tracksBySection.Count == 0)
        {
            Logger.Warning("No tracks found for any sections in publication {PublicationCode} ({LanguageCode}). Skipping.", publicationCode, normalizedLanguageCode);
            return;
        }

        // Save sections and tracks (similar to Bible publications structure)
        // Save to database via persister if available, otherwise save to files
        if (dataPersister != null)
        {
            await dataPersister.SaveDramaPublication(normalizedLanguageCode, publicationCode, finalPublicationName, tracksBySection, sectionNames);
        }
        else
        {
            DramaFilePersistence.SaveDramaSectionsAndTracks(publicationCode, normalizedLanguageCode, tracksBySection, sectionNames);
        }

        Logger.Information("Saved {Count} sections for publication {PublicationCode} ({LanguageCode})", tracksBySection.Count, publicationCode, normalizedLanguageCode);
    }

    private async Task<(List<DramaTrack>? Tracks, string? SectionName)> HarvestSectionTracks(string sectionCode, string languageCode, string publicationCode)
    {
        try
        {
            var isVideo = PublicationTypeHelper.IsVideo(publicationCode);
            var fileFormat = isVideo ? "MP4" : "MP3";
            var harvestLink = $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?output=json&pub={sectionCode}&fileformat={fileFormat}&alllangs=0&langwritten={languageCode}";
            var jsonString = await DownloadUtility.GetAsync(harvestLink);

            return DramaTrackParser.ParseTracksFromGetPubMediaLinks(jsonString, sectionCode, languageCode, Logger, isVideo);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("404") || ex.Message.Contains("Response status code"))
        {
            Logger.Warning("Section {SectionCode} not available for language {LanguageCode}. Skipping.", sectionCode, languageCode);
            return (null, null);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to fetch tracks for section {SectionCode} in language {LanguageCode}. Skipping.", sectionCode, languageCode);
            return (null, null);
        }
    }

}
