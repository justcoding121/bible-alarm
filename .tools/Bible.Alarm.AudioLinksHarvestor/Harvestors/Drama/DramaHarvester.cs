#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

internal class DramaHarvester(ILogger logger, DownloadUtility downloadUtility)
{
    private const int MaxConcurrentLanguageDownloads = 8;

    /// <summary>
    /// Drama categories with their API category keys and display names.
    /// These are fetched from the Mediator API as categories, not individual publications.
    /// </summary>
    private static readonly Dictionary<string, string> DramaCategoryToNameMappings = new([
        new KeyValuePair<string, string>("Dramas", "Audio Bible Dramas"),
        new KeyValuePair<string, string>("DramaticBiblePublications", "Dramatic Bible Readings")
    ]);

    private static readonly HashSet<string> TestRunLanguageCodes = ["E", "MY"];

    internal async Task HarvestDramaLinks(bool isTestRun = false)
    {
        // Use case-insensitive dictionary for language codes
        var languageCodeToCategories = new ConcurrentDictionary<string, ConcurrentBag<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var category in DramaCategoryToNameMappings)
        {
            logger.Information("Harvesting Drama category: {CategoryName} ({CategoryKey})", category.Value, category.Key);

            await HarvestDramaCategory(
                category.Key,
                category.Value,
                languageCodeToCategories,
                isTestRun);
        }

        SaveDramaMetadata(languageCodeToCategories);
    }

    private async Task HarvestDramaCategory(
        string categoryKey,
        string categoryName,
        ConcurrentDictionary<string, ConcurrentBag<string>> languageCodeToCategories,
        bool isTestRun)
    {
        // First, fetch the category for English to get all available languages across all media items
        var englishCategoryUrl = $"{AppConstants.ApiEndpoints.JwOrgMediatorApiBaseUrl}/categories/E/{categoryKey}?detailed=1";
        string jsonString;

        try
        {
            jsonString = await downloadUtility.GetAsync(englishCategoryUrl);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to fetch category {CategoryKey} for English. Skipping.", categoryKey);
            return;
        }

        // Parse the category response to get all unique languages
        var languagesFromCategory = ExtractLanguagesFromCategory(jsonString);
        if (languagesFromCategory.Count == 0)
        {
            logger.Warning("No languages found for category {CategoryKey}. Skipping.", categoryKey);
            return;
        }

        // Filter for test run
        IEnumerable<string> languagesToProcess = isTestRun
            ? languagesFromCategory.Where(l => TestRunLanguageCodes.Contains(l))
            : languagesFromCategory;

        logger.Information("Found {Count} languages for category {CategoryName}", languagesToProcess.Count(), categoryName);

        // Process each language
        using var semaphore = new SemaphoreSlim(MaxConcurrentLanguageDownloads, MaxConcurrentLanguageDownloads);
        var tasks = languagesToProcess.Select(async languageCode =>
        {
            await semaphore.WaitAsync();
            try
            {
                await ProcessCategoryForLanguage(
                    categoryKey,
                    categoryName,
                    languageCode,
                    languageCodeToCategories);
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
            logger.Error(ex, "Failed to parse languages from category JSON");
        }

        return languages;
    }

    private async Task ProcessCategoryForLanguage(
        string categoryKey,
        string categoryName,
        string languageCode,
        ConcurrentDictionary<string, ConcurrentBag<string>> languageCodeToCategories)
    {
        // Normalize language code to uppercase for consistent storage and comparison
        var normalizedLanguageCode = languageCode.ToUpperInvariant();
        logger.Information("Harvesting {CategoryName} for language {LanguageCode}", categoryName, normalizedLanguageCode);

        var categoryUrl = $"{AppConstants.ApiEndpoints.JwOrgMediatorApiBaseUrl}/categories/{normalizedLanguageCode}/{categoryKey}?detailed=1";
        string jsonString;

        try
        {
            jsonString = await downloadUtility.GetAsync(categoryUrl);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("404") || ex.Message.Contains("Response status code"))
        {
            logger.Warning("Category {CategoryKey} not available for language {LanguageCode}. Skipping.", categoryKey, normalizedLanguageCode);
            return;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to fetch category {CategoryKey} for language {LanguageCode}. Skipping.", categoryKey, normalizedLanguageCode);
            return;
        }

        var tracks = ParseCategoryTracks(jsonString, categoryKey, normalizedLanguageCode);
        if (tracks.Count == 0)
        {
            logger.Warning("No tracks found for category {CategoryKey} in language {LanguageCode}. Skipping.", categoryKey, normalizedLanguageCode);
            return;
        }

        // Track which categories are available for this language (case-insensitive dictionary handles normalization)
        var categoriesForLanguage = languageCodeToCategories.GetOrAdd(normalizedLanguageCode, _ => new ConcurrentBag<string>());
        if (!categoriesForLanguage.Contains(categoryKey))
        {
            categoriesForLanguage.Add(categoryKey);
        }

        // Save the tracks
        SaveDramaTracks(categoryKey, normalizedLanguageCode, tracks);

        logger.Information("Saved {Count} tracks for {CategoryName} in {LanguageCode}", tracks.Count, categoryName, normalizedLanguageCode);
    }

    private List<DramaTrack> ParseCategoryTracks(string jsonString, string categoryKey, string languageCode)
    {
        var tracks = new List<DramaTrack>();

        try
        {
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            if (!root.TryGetProperty("category", out var category))
            {
                return tracks;
            }

            if (!category.TryGetProperty("media", out var mediaArray))
            {
                return tracks;
            }

            var trackNumber = 1;
            foreach (var mediaItem in mediaArray.EnumerateArray())
            {
                var track = ParseMediaItem(mediaItem, trackNumber, categoryKey, languageCode);
                if (track != null)
                {
                    tracks.Add(track);
                    trackNumber++;
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to parse category tracks from JSON");
        }

        return tracks;
    }

    private DramaTrack? ParseMediaItem(JsonElement mediaItem, int trackNumber, string categoryKey, string languageCode)
    {
        // Get title
        if (!mediaItem.TryGetProperty("title", out var titleElement))
        {
            return null;
        }
        var title = titleElement.GetString() ?? "Unknown";

        // Skip audio descriptions
        if (title.Contains("audio descriptions", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // Get the first MP3 file URL
        if (!mediaItem.TryGetProperty("files", out var filesArray))
        {
            return null;
        }

        string? mp3Url = null;
        foreach (var file in filesArray.EnumerateArray())
        {
            if (!file.TryGetProperty("mimetype", out var mimeType))
            {
                continue;
            }

            if (mimeType.GetString() == "audio/mpeg")
            {
                if (file.TryGetProperty("progressiveDownloadURL", out var urlElement))
                {
                    mp3Url = urlElement.GetString();
                    break;
                }
            }
        }

        if (string.IsNullOrEmpty(mp3Url))
        {
            return null;
        }

        // Get the natural key for lookup path
        string lookUpPath;
        if (mediaItem.TryGetProperty("naturalKey", out var naturalKeyElement))
        {
            var naturalKey = naturalKeyElement.GetString() ?? "";
            // Extract publication code from naturalKey (e.g., "pub-dwj_E_1_AUDIO" -> "dwj")
            lookUpPath = $"?category={categoryKey}&lang={languageCode}&naturalKey={naturalKey}";
        }
        else
        {
            lookUpPath = $"?category={categoryKey}&lang={languageCode}&track={trackNumber}";
        }

        return new DramaTrack
        {
            Number = trackNumber,
            Title = title,
            Url = mp3Url,
            LookUpPath = lookUpPath
        };
    }

    private void SaveDramaTracks(string categoryKey, string languageCode, List<DramaTrack> tracks)
    {
        var dir = $"{DirectoryHelper.IndexDirectory}/media/Audio/Drama/{languageCode}/{categoryKey}";
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var file = $"{dir}/tracks.json";
        var tracksJson = JsonSerializer.Serialize(tracks.OrderBy(x => x.Number));
        File.WriteAllText(file, tracksJson);
    }

    private void SaveDramaMetadata(
        ConcurrentDictionary<string, ConcurrentBag<string>> languageCodeToCategories)
    {
        // Save publications (categories) for each language
        foreach (var kvp in languageCodeToCategories)
        {
            // Normalize language code to uppercase for consistent storage
            var languageCode = kvp.Key.ToUpperInvariant();
            var categories = kvp.Value.Distinct().ToList();

            var languageDir = $"{DirectoryHelper.IndexDirectory}/media/Audio/Drama/{languageCode}";
            if (!Directory.Exists(languageDir))
            {
                Directory.CreateDirectory(languageDir);
            }

            var publicationsJson = JsonSerializer.Serialize(
                categories.Select(c => new Publication
                {
                    Code = c,
                    Name = DramaCategoryToNameMappings.GetValueOrDefault(c, c)
                }).OrderBy(x => x.Code));

            File.WriteAllText($"{languageDir}/publications.json", publicationsJson);
        }

        // Save languages.json - store only codes, names will be looked up from Language table during seeding
        var dramaDir = $"{DirectoryHelper.IndexDirectory}/media/Audio/Drama";
        if (!Directory.Exists(dramaDir))
        {
            Directory.CreateDirectory(dramaDir);
        }

        // Store just the language codes (normalized to uppercase)
        // During DB seeding, names will be looked up from the existing Language table
        var languagesJson = JsonSerializer.Serialize(
            languageCodeToCategories.Keys.Select(code => new Language
            {
                Code = code.ToUpperInvariant(),
                Name = code.ToUpperInvariant() // Placeholder - actual name comes from Language table during seeding
            }).OrderBy(x => x.Code));

        File.WriteAllText($"{dramaDir}/languages.json", languagesJson);

        logger.Information("Saved drama metadata for {Count} languages", languageCodeToCategories.Count);
    }
}
