#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.AudioLinksHarvestor.Models;
using Bible.Alarm.AudioLinksHarvestor.Models.Drama;
using Bible.Alarm.AudioLinksHarvestor.Utility;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Serilog;
using DirectoryHelper = Bible.Alarm.AudioLinksHarvestor.Utility.DirectoryHelper;

namespace Bible.Alarm.AudioLinksHarvestor.Harvestors.Drama;

internal class DramaHarvester : BaseHarvester
{
    private const int MaxConcurrentSectionDownloads = 8;
    private readonly IDataPersister? dataPersister;

    public DramaHarvester(ILogger logger, DownloadUtility downloadUtility, IDataPersister? dataPersister = null)
        : base(logger, downloadUtility)
    {
        this.dataPersister = dataPersister;
    }

    /// <summary>
    /// Drama publication code to name mappings (for logging/fallback).
    /// Codes come from centralized JwSourceHelper.DramaCategoryCodes.
    /// </summary>
    private static readonly Dictionary<string, string> DramaPubCodeToNameMapping = new([
        new KeyValuePair<string, string>("Dramas", "Bible Dramas"),
        new KeyValuePair<string, string>("DramaticBibleReadings", "Dramatic Bible Readings")
    ]);

    /// <summary>
    /// Localized category names: (languageCode, categoryKey) -> localizedName
    /// </summary>
    private readonly ConcurrentDictionary<(string LanguageCode, string CategoryKey), string> localizedCategoryNames = new();


    internal async Task HarvestDramaLinks(bool isTestRun = false)
    {
        // Track publications per language: languageCode -> set of publication codes
        var languageCodeToPublications = new ConcurrentDictionary<string, ConcurrentDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        // Harvest each drama publication from the "Dramas" category (codes from centralized JwSourceHelper)
        foreach (var publicationCode in JwSourceHelper.DramaCategoryCodes)
        {
            var publicationName = DramaPubCodeToNameMapping.GetValueOrDefault(publicationCode, publicationCode);
            Logger.Information("Harvesting Drama publication: {PublicationName} ({PublicationCode})", publicationName, publicationCode);

            await HarvestDramaPublication(
                publicationCode,
                publicationName,
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
        // First, fetch the publication (which is accessed via category API) for English to get all available languages
        var englishCategoryUrl = $"{AppConstants.ApiEndpoints.JwOrgMediatorApiBaseUrl}/categories/E/{publicationCode}?detailed=1";
        string jsonString;

        try
        {
            jsonString = await DownloadUtility.GetAsync(englishCategoryUrl);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to fetch publication {PublicationCode} for English. Skipping.", publicationCode);
            return;
        }

        // Parse the category response to get all unique languages
        var languagesFromCategory = ExtractLanguagesFromCategory(jsonString);
        if (languagesFromCategory.Count == 0)
        {
            Logger.Warning("No languages found for publication {PublicationCode}. Skipping.", publicationCode);
            return;
        }

        // Extract language info from English category response
        var discoveredLanguages = ExtractLanguageInfoFromCategory(jsonString, languagesFromCategory);

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

    private HashSet<string> ExtractLanguagesFromCategory(string jsonString)
    {
        // Use case-insensitive HashSet to avoid duplicates from case differences
        var languages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            if (!root.TryGetProperty("category", out var category))
            {
                return languages;
            }

            if (!category.TryGetProperty("media", out var mediaArray))
            {
                return languages;
            }

            foreach (var mediaItem in mediaArray.EnumerateArray())
            {
                if (!mediaItem.TryGetProperty("availableLanguages", out var availableLanguages))
                {
                    continue;
                }

                foreach (var lang in availableLanguages.EnumerateArray())
                {
                    var langCode = lang.GetString();
                    if (!string.IsNullOrEmpty(langCode))
                    {
                        // Normalize to uppercase for consistent storage
                        languages.Add(langCode.ToUpperInvariant());
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to parse languages from category JSON");
        }

        return languages;
    }

    private Dictionary<string, LanguageInfo> ExtractLanguageInfoFromCategory(string jsonString, HashSet<string> languageCodes)
    {
        var languageInfoMap = new Dictionary<string, LanguageInfo>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            if (!root.TryGetProperty("category", out var category))
            {
                return languageInfoMap;
            }

            // Try to get language info from category.language if available
            if (category.TryGetProperty("language", out var languageElement))
            {
                var direction = "ltr";
                if (languageElement.TryGetProperty("direction", out var dirElement))
                {
                    direction = dirElement.GetString() ?? "ltr";
                }

                string? name = null;
                if (languageElement.TryGetProperty("name", out var nameElement))
                {
                    var rawName = nameElement.GetString();
                    name = rawName != null ? WebUtility.HtmlDecode(rawName) : null;
                }

                // This is for English, add it
                if (!string.IsNullOrEmpty(name))
                {
                    languageInfoMap["E"] = new LanguageInfo(name, direction);
                }
            }

            // For other languages, we'll use defaults (name = code, direction = ltr)
            // They can be updated when fetched on-demand
            foreach (var langCode in languageCodes)
            {
                if (!languageInfoMap.ContainsKey(langCode))
                {
                    languageInfoMap[langCode] = new LanguageInfo(langCode, "ltr");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to extract language info from category JSON");
        }

        return languageInfoMap;
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

        // Access publication via category API (publication code is used as category key in Mediator API)
        var categoryUrl = $"{AppConstants.ApiEndpoints.JwOrgMediatorApiBaseUrl}/categories/{normalizedLanguageCode}/{publicationCode}?detailed=1";
        string jsonString;

        try
        {
            jsonString = await DownloadUtility.GetAsync(categoryUrl);
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

        // Extract section codes from Mediator API response
        var (sectionCodes, localizedPublicationName) = ExtractSectionCodesFromCategory(jsonString, publicationCode, normalizedLanguageCode);
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
                var (tracks, sectionName) = await HarvestSectionTracks(sectionCode, normalizedLanguageCode);
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
            SaveDramaSectionsAndTracks(publicationCode, normalizedLanguageCode, tracksBySection, sectionNames);
        }

        Logger.Information("Saved {Count} sections for publication {PublicationCode} ({LanguageCode})", tracksBySection.Count, publicationCode, normalizedLanguageCode);
    }

    private (HashSet<string> SectionCodes, string? LocalizedPublicationName) ExtractSectionCodesFromCategory(string jsonString, string publicationCode, string languageCode)
    {
        var sectionCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? localizedPublicationName = null;

        try
        {
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            if (!root.TryGetProperty("category", out var category))
            {
                return (sectionCodes, null);
            }

            // Extract localized publication name
            if (category.TryGetProperty("name", out var nameElement))
            {
                var rawName = nameElement.GetString();
                // Decode HTML entities like &nbsp; to proper characters and replace non-breaking spaces with regular spaces
                localizedPublicationName = rawName != null ? WebUtility.HtmlDecode(rawName).Replace('\u00A0', ' ') : null;
            }

            if (!category.TryGetProperty("media", out var mediaArray))
            {
                return (sectionCodes, localizedPublicationName);
            }

            foreach (var mediaItem in mediaArray.EnumerateArray())
            {
                // Extract section code from naturalKey
                // Pattern: "pub-{sectionCode}_{lang}_{number}_AUDIO"
                // For example: "pub-iaoh_E_12_AUDIO" -> section code is "iaoh"
                string? sectionCode = null;
                if (mediaItem.TryGetProperty("naturalKey", out var naturalKeyElement))
                {
                    var naturalKey = naturalKeyElement.GetString() ?? "";
                    if (naturalKey.StartsWith("pub-", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = naturalKey.Split('_');
                        if (parts.Length > 0)
                        {
                            sectionCode = parts[0].Substring(4); // Remove "pub-" prefix
                        }
                    }
                }

                // If no section code found, skip this item
                if (string.IsNullOrEmpty(sectionCode))
                {
                    Logger.Warning("Could not extract section code from naturalKey in publication {PublicationCode} for language {LanguageCode}. Skipping.", publicationCode, languageCode);
                    continue;
                }

                sectionCodes.Add(sectionCode);
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to extract section codes from category JSON");
        }

        return (sectionCodes, localizedPublicationName);
    }

    private async Task<(List<DramaTrack>? Tracks, string? SectionName)> HarvestSectionTracks(string sectionCode, string languageCode)
    {
        try
        {
            // Use GETPUBMEDIALINKS to get tracks for this section
            var harvestLink = $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?output=json&pub={sectionCode}&fileformat=MP3&alllangs=0&langwritten={languageCode}";
            var jsonString = await DownloadUtility.GetAsync(harvestLink);

            return ParseTracksFromGetPubMediaLinks(jsonString, sectionCode, languageCode);
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

    private (List<DramaTrack>? Tracks, string? SectionName) ParseTracksFromGetPubMediaLinks(string jsonString, string sectionCode, string languageCode)
    {
        var tracks = new List<DramaTrack>();
        string? sectionName = null;

        try
        {
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("files", out var filesElement))
            {
                return (null, null);
            }

            // Extract section name from pubName field
            if (root.TryGetProperty("pubName", out var pubNameElement))
            {
                var rawName = pubNameElement.GetString();
                // Decode HTML entities like &nbsp; to proper characters and replace non-breaking spaces with regular spaces
                sectionName = rawName != null ? WebUtility.HtmlDecode(rawName).Replace('\u00A0', ' ') : null;
            }

            if (!filesElement.TryGetProperty(languageCode, out var languageFiles) ||
                !languageFiles.TryGetProperty("MP3", out var mp3Files))
            {
                return (null, sectionName);
            }

            var trackNumber = 1;
            foreach (var trackFile in mp3Files.EnumerateArray())
            {
                if (!trackFile.TryGetProperty("file", out var fileElement))
                {
                    continue;
                }

                // Handle both cases: file can be a string (direct URL) or an object with a "url" property
                string? url = null;
                if (fileElement.ValueKind == JsonValueKind.String)
                {
                    url = fileElement.GetString();
                }
                else if (fileElement.ValueKind == JsonValueKind.Object && fileElement.TryGetProperty("url", out var urlElement))
                {
                    url = urlElement.GetString();
                }

                if (string.IsNullOrEmpty(url))
                {
                    continue;
                }

                // Get track title
                string title = "Unknown";
                if (trackFile.TryGetProperty("title", out var titleElement))
                {
                    // Handle both cases: title can be a string or an object
                    if (titleElement.ValueKind == JsonValueKind.String)
                    {
                        var rawTitle = titleElement.GetString();
                        title = rawTitle != null ? WebUtility.HtmlDecode(rawTitle).Replace('\u00A0', ' ') : "Unknown";
                    }
                    else if (titleElement.ValueKind == JsonValueKind.Object && titleElement.TryGetProperty("text", out var titleTextElement))
                    {
                        var rawTitle = titleTextElement.GetString();
                        title = rawTitle != null ? WebUtility.HtmlDecode(rawTitle).Replace('\u00A0', ' ') : "Unknown";
                    }
                }

                // Build lookup path using GETPUBMEDIALINKS format
                var lookUpPath = $"?output=json&pub={sectionCode}&fileformat=MP3&langwritten={languageCode}&track={trackNumber}";

                tracks.Add(new DramaTrack
                {
                    Number = trackNumber,
                    Title = title,
                    Url = url,
                    LookUpPath = lookUpPath
                });

                trackNumber++;
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to parse tracks from GETPUBMEDIALINKS JSON for section {SectionCode}", sectionCode);
            return (null, null);
        }

        return tracks.Count > 0 ? (tracks, sectionName) : (null, sectionName);
    }


    private void SaveDramaSectionsAndTracks(string publicationCode, string languageCode, Dictionary<string, List<DramaTrack>> tracksBySection, Dictionary<string, string> sectionNames)
    {
        // Unified structure: media/Dramas/{languageCode}/{publicationCode}/sections.json
        // and media/Dramas/{languageCode}/{publicationCode}/{sectionCode}/tracks.json
        var normalizedLanguageCode = languageCode.ToUpperInvariant();
        var normalizedPublicationCode = publicationCode.ToUpperInvariant();
        var publicationDir = $"{DirectoryHelper.IndexDirectory}/media/Dramas/{normalizedLanguageCode}/{normalizedPublicationCode}";
        
        if (!Directory.Exists(publicationDir))
        {
            Directory.CreateDirectory(publicationDir);
        }

        // Save sections.json with section codes and names
        // Use section name from GETPUBMEDIALINKS if available, otherwise fall back to section code
        var sections = tracksBySection.Select((kvp, index) => new
        {
            Code = kvp.Key,
            Name = sectionNames.TryGetValue(kvp.Key, out var name) && !string.IsNullOrEmpty(name) ? name : kvp.Key,
            Number = index + 1 // Sequential number for ordering
        }).OrderBy(x => x.Code).ToList();

        var sectionsJson = JsonSerializer.Serialize(sections.Select(s => new
        {
            Code = s.Code,
            Name = s.Name,
            Number = s.Number
        }));
        File.WriteAllText($"{publicationDir}/sections.json", sectionsJson);

        // Save tracks for each section
        foreach (var sectionEntry in tracksBySection)
        {
            var sectionCode = sectionEntry.Key;
            var tracks = sectionEntry.Value;
            var normalizedSectionCode = sectionCode.ToUpperInvariant();
            var sectionDir = $"{publicationDir}/{normalizedSectionCode}";
            
            if (!Directory.Exists(sectionDir))
            {
                Directory.CreateDirectory(sectionDir);
            }

            var tracksJson = JsonSerializer.Serialize(tracks.OrderBy(x => x.Number));
            File.WriteAllText($"{sectionDir}/tracks.json", tracksJson);
        }
    }

}
