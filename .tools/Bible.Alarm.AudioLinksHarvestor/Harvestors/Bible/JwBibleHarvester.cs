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
using Bible.Alarm.AudioLinksHarvestor.Models.BiblePublications;
using Bible.Alarm.AudioLinksHarvestor.Utility;
using Bible.Alarm.Shared.Constants;
using Serilog;

namespace Bible.Alarm.AudioLinksHarvestor.Harvestors.Bible;

internal class JwBibleHarvester(ILogger logger, DownloadUtility downloadUtility)
{
    private const int MaxConcurrentLanguageDownloads = 8;
    private static readonly HashSet<string> TestRunLanguageCodes = ["E", "MY", "A"]; // A = Arabic, not AR (Bambara)

    /// <summary>
    /// Dictionary to store localized publication names: (languageCode, publicationCode) -> localizedName
    /// </summary>
    private readonly ConcurrentDictionary<(string LanguageCode, string PublicationCode), string> localizedPublicationNames = new();

    /// <summary>
    /// Gets the localized publication names collected during harvesting.
    /// Key: (languageCode, publicationCode), Value: localized publication name
    /// </summary>
    public IReadOnlyDictionary<(string LanguageCode, string PublicationCode), string> LocalizedPublicationNames => localizedPublicationNames;

    internal async Task HarvestBibleLinks(
        Dictionary<string, string> biblePublicationCodeToNameMappings,
        ConcurrentDictionary<string, LanguageInfo> languageCodeToInfoMappings,
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
                languageCodeToInfoMappings,
                languageCodeToEditionsMapping);
        }
    }

    private async Task<Dictionary<string, LanguageInfo>?> GetFilteredLanguages(string publicationCode, string publicationName, bool isTestRun)
    {
        Dictionary<string, LanguageInfo> discoveredLanguages;
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
            .Where(lang => !ShouldSkipLanguage(lang.Value.Name))
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
        Dictionary<string, LanguageInfo> filteredLanguages,
        string publicationCode,
        string publicationName,
        ConcurrentDictionary<string, LanguageInfo> languageCodeToInfoMappings,
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
                    languageCodeToInfoMappings,
                    languageCodeToEditionsMapping);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to harvest Bible links for {PublicationName} ({PublicationCode}) in {Language} ({LanguageCode}).", publicationName, publicationCode, langEntry.Value.Name, langEntry.Key);
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
        LanguageInfo languageInfo,
        string publicationCode,
        string publicationName,
        ConcurrentDictionary<string, LanguageInfo> languageCodeToInfoMappings,
        ConcurrentDictionary<string, List<string>> languageCodeToEditionsMapping)
    {
        languageCodeToInfoMappings.TryAdd(languageCode, languageInfo);

        logger.Information("Harvesting Bible track links for {PublicationName} of {Language} language.", publicationName, languageInfo.Name);
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


    private async Task<Dictionary<string, LanguageInfo>> DiscoverLanguagesFromApi(string publicationCode)
    {
        // Use case-insensitive dictionary to avoid duplicates from case differences
        var discoveredLanguages = new Dictionary<string, LanguageInfo>(StringComparer.OrdinalIgnoreCase);
        // Use txtCMSLang=E for language discovery since we need consistent English language names
        var harvestLink = $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?output=json&pub={publicationCode}&booknum=1&fileformat=MP3&alllangs=1&langwritten=E&txtCMSLang=E";

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

            var rawLanguage = nameElement.GetString();
            // Decode HTML entities like &nbsp; to proper characters
            var language = rawLanguage != null ? WebUtility.HtmlDecode(rawLanguage) : null;
            if (string.IsNullOrEmpty(language))
            {
                continue;
            }

            // Extract direction (defaults to "ltr" if not present)
            var direction = "ltr";
            if (item.Value.TryGetProperty("direction", out var directionElement))
            {
                direction = directionElement.GetString() ?? "ltr";
            }

            discoveredLanguages[languageCode] = new LanguageInfo(language, direction);
        }

        return discoveredLanguages;
    }

    private async Task<bool> HarvestBibleLinks(string languageCode, string publicationCode)
    {
        var sectionsDirectory = $"{DirectoryHelper.IndexDirectory}/media/Audio/Bible/{languageCode}/{publicationCode}";
        var sectionsIndex = $"{sectionsDirectory}/sections.json";

        var sectionNumberSectionMap = new Dictionary<int, BiblePublicationSection>();
        var sectionNumberTrackMap = new Dictionary<int, Dictionary<int, BiblePublicationTrack>>();
        string? localizedPublicationName = null;

        var sectionNumber = 1;
        // Use txtCMSLang={languageCode} to get localized publication names, section names, and track titles
        var harvestLink = $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?output=json&pub={publicationCode}&booknum={sectionNumber}&fileformat=MP3&alllangs=0&langwritten={languageCode}&txtCMSLang={languageCode}";

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

                // Extract section name from root pubName field (localized book name, e.g., "ഉൽപത്തി" for Genesis in Malayalam)
                string? sectionName = null;
                if (doc.RootElement.TryGetProperty("pubName", out var pubNameElement))
                {
                    var rawName = pubNameElement.GetString();
                    // Decode HTML entities like &nbsp; to proper characters
                    sectionName = rawName != null ? WebUtility.HtmlDecode(rawName) : null;
                }

                // Extract localized publication name from parentPubName field (e.g., "വിശുദ്ധ തിരുവെഴുത്തുകള്‍—പുതിയ ലോക ഭാഷാന്തരം" in Malayalam)
                // Only capture it once per language/publication combination
                if (localizedPublicationName == null && doc.RootElement.TryGetProperty("parentPubName", out var parentPubNameElement))
                {
                    var rawName = parentPubNameElement.GetString();
                    // Decode HTML entities like &nbsp; to proper characters
                    localizedPublicationName = rawName != null ? WebUtility.HtmlDecode(rawName) : null;
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

            // Store localized publication name if we captured it
            if (!string.IsNullOrEmpty(localizedPublicationName))
            {
                localizedPublicationNames[(languageCode, publicationCode)] = localizedPublicationName;
            }

            return true;
        }

        return false;
    }

    private static void ProcessSectionFiles(
        JsonElement sectionFiles,
        JsonElement root,
        Dictionary<int, BiblePublicationSection> sectionNumberSectionMap,
        Dictionary<int, Dictionary<int, BiblePublicationTrack>> sectionNumberTrackMap,
        ref int sectionNumber,
        string languageCode,
        string? sectionName)
    {
        foreach (var sectionFile in sectionFiles.EnumerateArray())
        {
            if (!TryExtractSectionFileData(sectionFile, out var url, out var track, out var fileSectionNumber, out var title))
            {
                continue;
            }

            if (track == 0 || url.EndsWith(".zip"))
            {
                continue;
            }

            sectionNumber = fileSectionNumber;
            EnsureSectionExists(root, sectionNumber, sectionNumberSectionMap, languageCode, sectionName);
            AddTrackIfNotExists(sectionNumber, track, url, title, sectionNumberTrackMap);
        }
    }

    private static bool TryExtractSectionFileData(JsonElement sectionFile, out string url, out int track, out int sectionNumber, out string title)
    {
        url = string.Empty;
        track = 0;
        sectionNumber = 0;
        title = string.Empty;

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

        if (!sectionFile.TryGetProperty("booknum", out var sectionNumElement))
        {
            return false;
        }

        sectionNumber = sectionNumElement.GetInt32();

        // Extract localized track title (e.g., "Chapter 1" in English, "അധ്യായം 1" in Malayalam)
        if (sectionFile.TryGetProperty("title", out var titleElement))
        {
            var rawTitle = titleElement.GetString();
            // Decode HTML entities like &nbsp; to proper characters
            title = rawTitle != null ? WebUtility.HtmlDecode(rawTitle) : string.Empty;
        }

        return true;
    }

    private static void EnsureSectionExists(
        JsonElement root,
        int sectionNumber,
        Dictionary<int, BiblePublicationSection> sectionNumberSectionMap,
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
                var rawName = pubNameElement.GetString();
                // Decode HTML entities like &nbsp; to proper characters
                sectionName = rawName != null ? WebUtility.HtmlDecode(rawName) : null;
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
                    var rawTitle = titleElement.GetString();
                    // Decode HTML entities and extract section name
                    var decodedTitle = rawTitle != null ? WebUtility.HtmlDecode(rawTitle) : null;
                    sectionName = decodedTitle != null ? GetSectionNameFromTitle(decodedTitle, languageCode, sectionNumber) : null;
                }
            }
        }

        if (string.IsNullOrEmpty(sectionName))
        {
            return;
        }

        sectionNumberSectionMap[sectionNumber] = new BiblePublicationSection
        {
            Number = sectionNumber,
            Name = sectionName
        };
    }

    private static void AddTrackIfNotExists(
        int sectionNumber,
        int trackNumber,
        string url,
        string title,
        Dictionary<int, Dictionary<int, BiblePublicationTrack>> sectionNumberTrackMap)
    {
        if (!sectionNumberTrackMap.ContainsKey(sectionNumber))
        {
            sectionNumberTrackMap[sectionNumber] = new Dictionary<int, BiblePublicationTrack>();
        }

        if (!sectionNumberTrackMap[sectionNumber].ContainsKey(trackNumber))
        {
            sectionNumberTrackMap[sectionNumber].Add(trackNumber, new BiblePublicationTrack
            {
                Number = trackNumber,
                Url = url,
                Title = title
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
        // Use txtCMSLang={languageCode} to get localized publication names, section names, and track titles
        harvestLink = $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?output=json&pub={publicationCode}&booknum={sectionNumber}&fileformat=MP3&alllangs=0&langwritten={languageCode}&txtCMSLang={languageCode}";
    }

    private static void SaveSectionsAndTracks(
        string sectionsDirectory,
        string sectionsIndex,
        Dictionary<int, BiblePublicationSection> sectionNumberSectionMap,
        Dictionary<int, Dictionary<int, BiblePublicationTrack>> sectionNumberTrackMap)
    {
        if (!Directory.Exists(sectionsDirectory))
        {
            Directory.CreateDirectory(sectionsDirectory);
        }

        File.WriteAllText(sectionsIndex, JsonSerializer.Serialize(sectionNumberSectionMap.Select(x =>
            new BiblePublicationSection
            {
                Number = x.Key,
                Name = x.Value.Name
            }).OrderBy(x => x.Number)));

        foreach (var section in sectionNumberSectionMap)
        {
            var directory = $"{sectionsDirectory}/{section.Value.Number}";
            DirectoryHelper.Ensure(directory);

            var trackIndex = $"{directory}/tracks.json";
            // Serialize tracks with Number, Url, and Title (localized chapter name)
            File.WriteAllText(trackIndex, JsonSerializer.Serialize(
                sectionNumberTrackMap[section.Key]
                    .Select(x => new BiblePublicationTrack
                    {
                        Number = x.Value.Number,
                        Url = x.Value.Url,
                        Title = x.Value.Title
                    })
                    .OrderBy(x => x.Number)
                    .ToList()));
        }
    }
}
