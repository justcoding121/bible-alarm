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
using Serilog;

namespace Bible.Alarm.AudioLinksHarvestor.Harvestors.Drama;

internal class DramaHarvester : BaseHarvester
{
    private const int MaxConcurrentSectionDownloads = 8;

    public DramaHarvester(ILogger logger, DownloadUtility downloadUtility)
        : base(logger, downloadUtility)
    {
    }

    /// <summary>
    /// Drama publication codes with their display names (English fallback).
    /// These are publications under the "Dramas" category in the Mediator API.
    /// Note: "gnj" (Good News According to Jesus) is harvested separately by VideoHarvester.
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

        // Harvest each drama publication from the "Dramas" category
        foreach (var publication in DramaPubCodeToNameMapping)
        {
            Logger.Information("Harvesting Drama publication: {PublicationName} ({PublicationCode})", publication.Value, publication.Key);

            await HarvestDramaPublication(
                publication.Key,
                publication.Value,
                languageCodeToPublications,
                isTestRun);
        }

        SaveDramaMetadata(languageCodeToPublications);
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

        // Filter for test run
        IEnumerable<string> languagesToProcess = isTestRun
            ? languagesFromCategory.Where(l => TestRunLanguageCodes.Contains(l))
            : languagesFromCategory;

        Logger.Information("Found {Count} languages for publication {PublicationName}", languagesToProcess.Count(), publicationName);

        // Process each language
        using var semaphore = new SemaphoreSlim(MaxConcurrentLanguageDownloads, MaxConcurrentLanguageDownloads);
        var tasks = languagesToProcess.Select(async languageCode =>
        {
            await semaphore.WaitAsync();
            try
            {
                await ProcessPublicationForLanguage(
                    publicationCode,
                    publicationName,
                    languageCode,
                    languageCodeToPublications);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
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
        SaveDramaSectionsAndTracks(publicationCode, normalizedLanguageCode, tracksBySection, sectionNames);

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

    private void SaveDramaMetadata(
        ConcurrentDictionary<string, ConcurrentDictionary<string, string>> languageCodeToPublications)
    {
        // Save publications for each language
        foreach (var kvp in languageCodeToPublications)
        {
            // Normalize language code to uppercase for consistent storage
            var languageCode = kvp.Key.ToUpperInvariant();
            var publications = kvp.Value;

            // Unified structure: media/Dramas/{languageCode} (no Audio/Video prefix)
            var languageDir = $"{DirectoryHelper.IndexDirectory}/media/Dramas/{languageCode}";
            if (!Directory.Exists(languageDir))
            {
                Directory.CreateDirectory(languageDir);
            }

            var publicationsJson = JsonSerializer.Serialize(
                publications.Select(pub =>
                {
                    return new Publication
                    {
                        Code = pub.Key,
                        Name = pub.Value
                    };
                }).OrderBy(x => x.Code));

            File.WriteAllText($"{languageDir}/publications.json", publicationsJson);
        }

        // Save languages.json - store only codes, names and directions will be looked up from Language table during seeding
        // Unified structure: media/Dramas (no Audio/Video prefix)
        var dramaDir = $"{DirectoryHelper.IndexDirectory}/media/Dramas";
        if (!Directory.Exists(dramaDir))
        {
            Directory.CreateDirectory(dramaDir);
        }

        // Store just the language codes (normalized to uppercase)
        // During DB seeding, names and directions will be looked up from the existing Language table
        var languagesJson = JsonSerializer.Serialize(
            languageCodeToPublications.Keys.Select(code => new Language
            {
                Code = code.ToUpperInvariant(),
                Name = code.ToUpperInvariant(), // Placeholder - actual name comes from Language table during seeding
                Direction = "ltr" // Placeholder - actual direction comes from Language table during seeding
            }).OrderBy(x => x.Code));

        File.WriteAllText($"{dramaDir}/languages.json", languagesJson);

        Logger.Information("Saved drama metadata for {Count} languages", languageCodeToPublications.Count);
    }
}
