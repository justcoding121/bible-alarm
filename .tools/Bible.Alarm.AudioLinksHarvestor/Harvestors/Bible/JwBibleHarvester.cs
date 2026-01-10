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
using Bible.Alarm.AudioLinksHarvestor.Models.Bible;
using Bible.Alarm.AudioLinksHarvestor.Utility;
using Bible.Alarm.Shared.Constants;
using Serilog;

namespace Bible.Alarm.AudioLinksHarvestor.Harvestors.Bible;

internal class JwBibleHarvester(ILogger logger, DownloadUtility downloadUtility)
{
    private const int MaxConcurrentLanguageDownloads = 8;
    private static readonly HashSet<string> TestRunLanguageCodes = ["E", "MY"];

    internal async Task HarvestBibleLinks(
        Dictionary<string, string> biblePublicationCodeToNameMappings,
        ConcurrentDictionary<string, string> languageCodeToNameMappings,
        ConcurrentDictionary<string, List<string>> languageCodeToEditionsMapping,
        bool isTestRun = false)
    {
        foreach (var publication in biblePublicationCodeToNameMappings)
        {
            var publicationCode = publication.Key;
            logger.Information("Starting harvest for publication: {PublicationCode} ({PublicationName})", publicationCode, publication.Value);

            var filteredLanguages = await GetFilteredLanguages(publicationCode, publication.Value, isTestRun);
            if (filteredLanguages == null || filteredLanguages.Count == 0)
            {
                continue;
            }

            await ProcessLanguagesForPublication(
                filteredLanguages,
                publicationCode,
                publication.Value,
                languageCodeToNameMappings,
                languageCodeToEditionsMapping);
        }
    }

    private async Task<Dictionary<string, string>?> GetFilteredLanguages(string publicationCode, string publicationName, bool isTestRun)
    {
        Dictionary<string, string> discoveredLanguages;
        try
        {
            discoveredLanguages = await DiscoverLanguagesFromApi(publicationCode);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("Response status code"))
        {
            return null;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to discover languages for publication {PublicationCode} ({PublicationName}). Skipping.", publicationCode, publicationName);
            return null;
        }

        if (discoveredLanguages.Count == 0)
        {
            return null;
        }

        var filteredLanguages = discoveredLanguages
            .Where(lang => !ShouldSkipLanguage(lang.Value))
            .ToDictionary(x => x.Key, x => x.Value);

        if (filteredLanguages.Count == 0)
        {
            return null;
        }

        if (isTestRun)
        {
            var testRunLanguages = filteredLanguages
                .Where(l => TestRunLanguageCodes.Contains(l.Key))
                .ToDictionary(l => l.Key, l => l.Value);

            if (testRunLanguages.Count > 0)
            {
                logger.Information("TEST RUN: Processing languages {Languages} for publication {PublicationCode}",
                    string.Join(", ", testRunLanguages.Keys), publicationCode);
                return testRunLanguages;
            }
            return null;
        }

        return filteredLanguages;
    }

    private async Task ProcessLanguagesForPublication(
        Dictionary<string, string> filteredLanguages,
        string publicationCode,
        string publicationName,
        ConcurrentDictionary<string, string> languageCodeToNameMappings,
        ConcurrentDictionary<string, List<string>> languageCodeToEditionsMapping)
    {
        using var semaphore = new SemaphoreSlim(MaxConcurrentLanguageDownloads, MaxConcurrentLanguageDownloads);
        var languageTasks = filteredLanguages.Select(async langEntry =>
        {
            await semaphore.WaitAsync();
            try
            {
                await ProcessLanguage(
                    langEntry.Key,
                    langEntry.Value,
                    publicationCode,
                    publicationName,
                    languageCodeToNameMappings,
                    languageCodeToEditionsMapping);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to harvest Bible links for {PublicationName} ({PublicationCode}) in {Language} ({LanguageCode}).", publicationName, publicationCode, langEntry.Value, langEntry.Key);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(languageTasks);
    }

    private async Task ProcessLanguage(
        string languageCode,
        string language,
        string publicationCode,
        string publicationName,
        ConcurrentDictionary<string, string> languageCodeToNameMappings,
        ConcurrentDictionary<string, List<string>> languageCodeToEditionsMapping)
    {
        languageCodeToNameMappings.TryAdd(languageCode, language);

        logger.Information("Harvesting Bible track links for {PublicationName} of {Language} language.", publicationName, language);
        await HarvestBibleLinks(languageCode, publicationCode);

        if (!languageCodeToEditionsMapping.TryAdd(languageCode, [publicationCode]))
        {
            languageCodeToEditionsMapping[languageCode].Add(publicationCode);
        }
    }

    private static bool ShouldSkipLanguage(string languageName)
    {
        if (string.IsNullOrWhiteSpace(languageName))
        {
            return false;
        }

        var lowerName = languageName.ToLowerInvariant();
        return lowerName.Contains("sign language");
    }


    private async Task<Dictionary<string, string>> DiscoverLanguagesFromApi(string publicationCode)
    {
        // Use case-insensitive dictionary to avoid duplicates from case differences
        var discoveredLanguages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var harvestLink = $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?output=json&pub={publicationCode}&sectionnum=1&fileformat=MP3&alllangs=1&langwritten=E&txtCMSLang=E";

        var jsonString = await downloadUtility.GetAsync(harvestLink);
        using var doc = JsonDocument.Parse(jsonString);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
        {
            return discoveredLanguages;
        }

        if (!root.TryGetProperty("languages", out var languages))
        {
            return discoveredLanguages;
        }

        foreach (var item in languages.EnumerateObject())
        {
            // Normalize language code to uppercase for consistent storage
            var languageCode = item.Name.ToUpperInvariant();

            if (!item.Value.TryGetProperty("name", out var nameElement))
            {
                continue;
            }

            var language = nameElement.GetString();
            if (string.IsNullOrEmpty(language))
            {
                continue;
            }

            discoveredLanguages[languageCode] = language;
        }

        return discoveredLanguages;
    }

    private async Task<bool> HarvestBibleLinks(string languageCode, string publicationCode)
    {
        var sectionsDirectory = $"{DirectoryHelper.IndexDirectory}/media/Audio/Bible/{languageCode}/{publicationCode}";
        var sectionsIndex = $"{sectionsDirectory}/sections.json";

        var sectionNumberSectionMap = new Dictionary<int, BibleSection>();
        var sectionNumberTrackMap = new Dictionary<int, Dictionary<int, BibleTrack>>();

        var sectionNumber = 1;
        var harvestLink = $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?output=json&pub={publicationCode}&sectionnum={sectionNumber}&fileformat=MP3&alllangs=0&langwritten={languageCode}&txtCMSLang=E";

        while (sectionNumber <= 66)
        {
            string jsonString;
            try
            {
                jsonString = await downloadUtility.GetAsync(harvestLink);
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("Response status code"))
            {
                AdvanceToNextSection(ref sectionNumber, ref harvestLink, publicationCode, languageCode);
                continue;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to fetch section {SectionNumber} for publication {PublicationCode} in language {LanguageCode}. Skipping to next section.", sectionNumber, publicationCode, languageCode);
                AdvanceToNextSection(ref sectionNumber, ref harvestLink, publicationCode, languageCode);
                continue;
            }

            JsonDocument? doc = null;
            JsonElement files = default;

            try
            {
                doc = JsonDocument.Parse(jsonString);
                var root = doc.RootElement;

                if (root.ValueKind != JsonValueKind.Object)
                {
                    AdvanceToNextSection(ref sectionNumber, ref harvestLink, publicationCode, languageCode);
                    doc?.Dispose();
                    continue;
                }

                if (!root.TryGetProperty("files", out files))
                {
                    AdvanceToNextSection(ref sectionNumber, ref harvestLink, publicationCode, languageCode);
                    doc?.Dispose();
                    continue;
                }
            }
            catch (Exception e)
            {
                if (e is JsonException or ArgumentException or KeyNotFoundException or InvalidOperationException)
                {
                    AdvanceToNextSection(ref sectionNumber, ref harvestLink, publicationCode, languageCode);
                    doc?.Dispose();
                    continue;
                }
                throw;
            }

            try
            {
                if (!files.TryGetProperty(languageCode, out var languageFiles))
                {
                    AdvanceToNextSection(ref sectionNumber, ref harvestLink, publicationCode, languageCode);
                    continue;
                }

                if (!languageFiles.TryGetProperty("MP3", out var sectionFiles))
                {
                    AdvanceToNextSection(ref sectionNumber, ref harvestLink, publicationCode, languageCode);
                    continue;
                }

                // Extract section name from root pubName field
                string? sectionName = null;
                if (doc.RootElement.TryGetProperty("pubName", out var pubNameElement))
                {
                    sectionName = pubNameElement.GetString();
                }

                ProcessSectionFiles(sectionFiles, doc.RootElement, sectionNumberSectionMap, sectionNumberTrackMap, ref sectionNumber, languageCode, sectionName);

                // Advance to next section after successful processing
                AdvanceToNextSection(ref sectionNumber, ref harvestLink, publicationCode, languageCode);
            }
            catch (Exception ex)
            {
                // Log and advance to next section on any exception during processing
                // This ensures we don't get stuck in an infinite loop
                logger.Warning(ex, "Error processing section files for section {SectionNumber}. Advancing to next section.", sectionNumber);
                AdvanceToNextSection(ref sectionNumber, ref harvestLink, publicationCode, languageCode);
            }
            finally
            {
                doc?.Dispose();
            }
        }

        // Check if any sections were successfully harvested
        // Note: sectionNumberSectionMap will have items if ProcessSectionFiles successfully processed any section files
        if (sectionNumberSectionMap.Count > 0)
        {
            SaveSectionsAndTracks(sectionsDirectory, sectionsIndex, sectionNumberSectionMap, sectionNumberTrackMap);
            return true;
        }

        return false;
    }

    private static void ProcessSectionFiles(
        JsonElement sectionFiles,
        JsonElement root,
        Dictionary<int, BibleSection> sectionNumberSectionMap,
        Dictionary<int, Dictionary<int, BibleTrack>> sectionNumberTrackMap,
        ref int sectionNumber,
        string languageCode,
        string? sectionName)
    {
        foreach (var sectionFile in sectionFiles.EnumerateArray())
        {
            if (!TryExtractSectionFileData(sectionFile, out var url, out var track, out var fileSectionNumber))
            {
                continue;
            }

            if (track == 0 || url.EndsWith(".zip"))
            {
                continue;
            }

            sectionNumber = fileSectionNumber;
            EnsureSectionExists(root, sectionNumber, sectionNumberSectionMap, languageCode, sectionName);
            AddTrackIfNotExists(sectionNumber, track, url, sectionNumberTrackMap);
        }
    }

    private static bool TryExtractSectionFileData(JsonElement sectionFile, out string url, out int track, out int sectionNumber)
    {
        url = string.Empty;
        track = 0;
        sectionNumber = 0;

        if (!sectionFile.TryGetProperty("file", out var fileElement) ||
            !fileElement.TryGetProperty("url", out var urlElement))
        {
            return false;
        }

        url = urlElement.GetString() ?? string.Empty;
        if (string.IsNullOrEmpty(url))
        {
            return false;
        }

        if (!sectionFile.TryGetProperty("track", out var trackElement))
        {
            return false;
        }

        track = trackElement.GetInt32();

        if (!sectionFile.TryGetProperty("sectionnum", out var sectionNumElement))
        {
            return false;
        }

        sectionNumber = sectionNumElement.GetInt32();
        return true;
    }

    private static void EnsureSectionExists(
        JsonElement root,
        int sectionNumber,
        Dictionary<int, BibleSection> sectionNumberSectionMap,
        string languageCode,
        string? sectionName)
    {
        if (sectionNumberSectionMap.ContainsKey(sectionNumber))
        {
            return;
        }

        // Use pubName from root if available, otherwise fall back to parsing from title
        if (string.IsNullOrEmpty(sectionName))
        {
            // Fallback: try to get from root pubName field
            if (root.TryGetProperty("pubName", out var pubNameElement))
            {
                sectionName = pubNameElement.GetString();
            }
        }

        // If still empty, try to get from first file title as last resort
        if (string.IsNullOrEmpty(sectionName))
        {
            if (root.TryGetProperty("files", out var files) &&
                files.TryGetProperty(languageCode, out var languageFiles) &&
                languageFiles.TryGetProperty("MP3", out var sectionFiles) &&
                sectionFiles.GetArrayLength() > 0)
            {
                var firstFile = sectionFiles[0];
                if (firstFile.TryGetProperty("title", out var titleElement))
                {
                    sectionName = GetSectionNameFromTitle(titleElement.GetString()!, languageCode, sectionNumber);
                }
            }
        }

        if (string.IsNullOrEmpty(sectionName))
        {
            return;
        }

        sectionNumberSectionMap[sectionNumber] = new BibleSection
        {
            Number = sectionNumber,
            Name = sectionName
        };
    }

    private static void AddTrackIfNotExists(
        int sectionNumber,
        int trackNumber,
        string url,
        Dictionary<int, Dictionary<int, BibleTrack>> sectionNumberTrackMap)
    {
        if (!sectionNumberTrackMap.ContainsKey(sectionNumber))
        {
            sectionNumberTrackMap[sectionNumber] = new Dictionary<int, BibleTrack>();
        }

        if (!sectionNumberTrackMap[sectionNumber].ContainsKey(trackNumber))
        {
            sectionNumberTrackMap[sectionNumber].Add(trackNumber, new BibleTrack
            {
                Number = trackNumber,
                Url = url,
            });
        }
    }

    private static string? GetSectionNameFromTitle(string title, string languageCode, int sectionNumber)
    {
        if (string.IsNullOrEmpty(title))
        {
            return null;
        }

        var parts = title.Split('-', 2);
        
        // If title starts with "Track", the section name is after the dash
        // Format: "Track X - Section Name"
        // Otherwise, the section name is before the dash
        // Format: "Section Name - Track X" or just "Section Name"
        string name;
        if (title.TrimStart().StartsWith("Track", StringComparison.OrdinalIgnoreCase) && parts.Length > 1)
        {
            name = parts[1].Trim();
        }
        else
        {
            name = parts[0].Trim();
        }
        
        return FormatSectionName(name, languageCode, sectionNumber);
    }

    private static string FormatSectionName(string name, string languageCode, int sectionNumber)
    {
        // For Malayalam (MY) language, remove "1" suffix from Psalms (section 19)
        if (languageCode == "MY" && sectionNumber == 19 && name.EndsWith(" 1", StringComparison.Ordinal))
        {
            return name.Substring(0, name.Length - 2).TrimEnd();
        }

        // Legacy fix: Convert "Psalm 1" to "Psalms" for other languages
        return name == "Psalm 1" ? "Psalms" : name;
    }

    private static void AdvanceToNextSection(ref int sectionNumber, ref string harvestLink, string publicationCode, string languageCode)
    {
        sectionNumber++;
        harvestLink = $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?output=json&pub={publicationCode}&sectionnum={sectionNumber}&fileformat=MP3&alllangs=0&langwritten={languageCode}&txtCMSLang=E";
    }

    private static void SaveSectionsAndTracks(
        string sectionsDirectory,
        string sectionsIndex,
        Dictionary<int, BibleSection> sectionNumberSectionMap,
        Dictionary<int, Dictionary<int, BibleTrack>> sectionNumberTrackMap)
    {
        if (!Directory.Exists(sectionsDirectory))
        {
            Directory.CreateDirectory(sectionsDirectory);
        }

        File.WriteAllText(sectionsIndex, JsonSerializer.Serialize(sectionNumberSectionMap.Select(x =>
            new BibleSection
            {
                Number = x.Key,
                Name = x.Value.Name
            }).OrderBy(x => x.Number)));

        foreach (var section in sectionNumberSectionMap)
        {
            var directory = $"{sectionsDirectory}/{section.Value.Number}";
            DirectoryHelper.Ensure(directory);

            var trackIndex = $"{directory}/tracks.json";
            File.WriteAllText(trackIndex, JsonSerializer.Serialize(
                sectionNumberTrackMap[section.Key]
                    .Select(x => x.Value)
                    .OrderBy(x => x.Number)
                    .ToList()));
        }
    }
}
