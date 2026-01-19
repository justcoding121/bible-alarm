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

internal class JwBibleHarvester : BaseHarvester
{
    private readonly IDataPersister? dataPersister;

    public JwBibleHarvester(ILogger logger, DownloadUtility downloadUtility, IDataPersister? dataPersister = null)
        : base(logger, downloadUtility)
    {
        this.dataPersister = dataPersister;
    }

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
            Logger.Information("Starting harvest for publication: {PublicationCode} ({PublicationName})", publicationCode, publication.Value);

            // Discover languages for each book (1-66) and save for English
            var allDiscoveredLanguages = await DiscoverLanguagesForAllBooks(publicationCode, publication.Value, isTestRun);
            
            if (allDiscoveredLanguages == null || allDiscoveredLanguages.Count == 0)
            {
                Logger.Warning("No languages discovered for publication {PublicationCode}. Skipping.", publicationCode);
                continue;
            }

            // Save language discovery results for English
            // Save language discovery
            if (dataPersister != null)
            {
                var languageCodeToNameMapping = allDiscoveredLanguages.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Name);
                await dataPersister.SaveLanguageDiscovery("E", publicationCode, languageCodeToNameMapping);
            }
            else
            {
                await SaveLanguageDiscoveryForEnglish(publicationCode, allDiscoveredLanguages);
            }

            // Save discovered languages for on-demand fetching (including English - it will be seeded separately)
            if (dataPersister != null)
            {
                await dataPersister.SavePublicationLanguages(publicationCode, allDiscoveredLanguages);
            }

            // Verify English (E) is available (it will be seeded separately after discovery)
            if (!allDiscoveredLanguages.TryGetValue("E", out var englishLanguageInfo))
            {
                Logger.Warning("English (E) not found in discovered languages for publication {PublicationCode}. Skipping.", publicationCode);
                continue;
            }

            // Add English to language mappings (for reference, but don't process it here)
            languageCodeToInfoMappings.TryAdd("E", englishLanguageInfo);
            if (!languageCodeToEditionsMapping.TryAdd("E", [publicationCode]))
            {
                languageCodeToEditionsMapping["E"].Add(publicationCode);
            }
        }
    }

    /// <summary>
    /// Filters discovered languages by excluding sign languages and applying test run filter if needed.
    /// Note: English (E) is included in processing to ensure it's added to languageCodeToEditionsMapping.
    /// </summary>
    private Dictionary<string, LanguageInfo>? FilterLanguages(Dictionary<string, LanguageInfo> discoveredLanguages, bool isTestRun)
    {
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
                Logger.Information("TEST RUN: Processing languages {Languages}",
                    string.Join(", ", testRunLanguages.Keys));
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
                Logger.Error(ex, "Failed to harvest Bible links for {PublicationName} ({PublicationCode}) in {Language} ({LanguageCode}).", publicationName, publicationCode, langEntry.Value.Name, langEntry.Key);
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
        Logger.Information("Processing {Language} ({LanguageCode}) for publication: {PublicationName} ({PublicationCode})", 
            languageInfo.Name, languageCode, publicationName, publicationCode);
        
        languageCodeToInfoMappings.TryAdd(languageCode, languageInfo);

        Logger.Information("Harvesting Bible track links for {PublicationName} of {Language} language.", publicationName, languageInfo.Name);
        await HarvestBibleLinks(languageCode, publicationCode, publicationName);

        if (!languageCodeToEditionsMapping.TryAdd(languageCode, [publicationCode]))
        {
            languageCodeToEditionsMapping[languageCode].Add(publicationCode);
        }
    }



    /// <summary>
    /// Discovers languages for each Bible book (1-66) using alllangs=1 and langwritten=E.
    /// Returns a consolidated dictionary of all unique languages discovered across all books.
    /// </summary>
    private async Task<Dictionary<string, LanguageInfo>?> DiscoverLanguagesForAllBooks(
        string publicationCode, 
        string publicationName, 
        bool isTestRun)
    {
        var allDiscoveredLanguages = new Dictionary<string, LanguageInfo>(StringComparer.OrdinalIgnoreCase);

        Logger.Information("Discovering languages for all books (1-66) in publication {PublicationCode}", publicationCode);

        // Discover languages for each book (1-66)
        for (int bookNum = 1; bookNum <= 66; bookNum++)
        {
            try
            {
                var bookLanguages = await DiscoverLanguagesForBook(publicationCode, bookNum);
                
                if (bookLanguages != null && bookLanguages.Count > 0)
                {
                    // Save section languages for this book
                    // The alllangs=1 response already lists only available languages, so no verification needed
                    if (dataPersister != null)
                    {
                        await dataPersister.SaveSectionLanguages(publicationCode, bookNum.ToString(), bookLanguages);
                    }

                    // Merge languages into consolidated dictionary
                    foreach (var lang in bookLanguages)
                    {
                        if (!allDiscoveredLanguages.ContainsKey(lang.Key))
                        {
                            allDiscoveredLanguages[lang.Key] = lang.Value;
                        }
                    }
                    
                    Logger.Debug("Book {BookNum}: Discovered {LanguageCount} languages (total unique: {TotalLanguages})", 
                        bookNum, bookLanguages.Count, allDiscoveredLanguages.Count);
                }
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("Response status code"))
            {
                Logger.Warning("Book {BookNum}: HTTP error during language discovery. Skipping.", bookNum);
                continue;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Book {BookNum}: Failed to discover languages. Skipping.", bookNum);
                continue;
            }
        }

        if (allDiscoveredLanguages.Count == 0)
        {
            Logger.Warning("No languages discovered for publication {PublicationCode} across all books", publicationCode);
            return null;
        }

        Logger.Information("Total unique languages discovered across all books for {PublicationCode}: {LanguageCount}", 
            publicationCode, allDiscoveredLanguages.Count);

        // Return discovered languages (alllangs=1 response already lists only available languages)
        return allDiscoveredLanguages;
    }

    /// <summary>
    /// Discovers languages for a specific book using alllangs=1 and langwritten=E.
    /// </summary>
    private async Task<Dictionary<string, LanguageInfo>?> DiscoverLanguagesForBook(
        string publicationCode, 
        int bookNum)
    {
        var discoveredLanguages = new Dictionary<string, LanguageInfo>(StringComparer.OrdinalIgnoreCase);
        
        // Use alllangs=1 and langwritten=E for discovery
        var harvestLink = $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?output=json&pub={publicationCode}&booknum={bookNum}&fileformat=MP3&alllangs=1&langwritten=E";

        var jsonString = await DownloadUtility.GetAsync(harvestLink);
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

    /// <summary>
    /// Saves language discovery results for English (E) as JSON.
    /// Structure: media/Bible/E/{publicationCode}/language-discovery.json
    /// </summary>
    private async Task SaveLanguageDiscoveryForEnglish(
        string publicationCode,
        Dictionary<string, LanguageInfo> discoveredLanguages)
    {
        // Normalize publication code for path consistency
        var normalizedPublicationCode = publicationCode.ToUpperInvariant();
        var englishDir = $"{DirectoryHelper.IndexDirectory}/media/Bible/E/{normalizedPublicationCode}";
        DirectoryHelper.Ensure(englishDir);

        var languageDiscoveryFile = Path.Combine(englishDir, "language-discovery.json");
        
        // Create a list of languages with Code, Name, and Direction
        var languageList = discoveredLanguages
            .Select(kvp => new
            {
                Code = kvp.Key,
                Name = kvp.Value.Name,
                Direction = kvp.Value.Direction
            })
            .OrderBy(x => x.Code)
            .ToList();

        var json = JsonSerializer.Serialize(languageList, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
        
        await File.WriteAllTextAsync(languageDiscoveryFile, json);
        
        Logger.Information("Saved language discovery for English: {LanguageCount} languages to {File}", 
            discoveredLanguages.Count, languageDiscoveryFile);
    }

    private async Task<bool> HarvestBibleLinks(string languageCode, string publicationCode, string publicationName)
    {
        // Normalize to uppercase for consistent file paths (cross-platform safety)
        var normalizedLanguageCode = languageCode.ToUpperInvariant();
        var normalizedPublicationCode = publicationCode.ToUpperInvariant();
        
        var sectionsDirectory = $"{DirectoryHelper.IndexDirectory}/media/Bible/{normalizedLanguageCode}/{normalizedPublicationCode}";
        var sectionsIndex = $"{sectionsDirectory}/sections.json";

        var sectionNumberSectionMap = new Dictionary<int, BiblePublicationSection>();
        var sectionNumberTrackMap = new Dictionary<int, Dictionary<int, BiblePublicationTrack>>();
        string? localizedPublicationName = null;

        var sectionNumber = 1;
        var harvestLink = $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?output=json&pub={publicationCode}&booknum={sectionNumber}&fileformat=MP3&alllangs=0&langwritten={languageCode}";

        while (sectionNumber <= 66)
        {
            string jsonString;
            try
            {
                jsonString = await DownloadUtility.GetAsync(harvestLink);
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("Response status code"))
            {
                AdvanceToNextSection(ref sectionNumber, ref harvestLink, publicationCode, languageCode);
                continue;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to fetch section {SectionNumber} for publication {PublicationCode} in language {LanguageCode}. Skipping to next section.", sectionNumber, publicationCode, languageCode);
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
                    // Decode HTML entities like &nbsp; to proper characters and replace non-breaking spaces with regular spaces
                    sectionName = rawName != null ? WebUtility.HtmlDecode(rawName).Replace('\u00A0', ' ') : null;
                }

                // Extract localized publication name from parentPubName field (e.g., "വിശുദ്ധ തിരുവെഴുത്തുകള്‍—പുതിയ ലോക ഭാഷാന്തരം" in Malayalam)
                // Only capture it once per language/publication combination
                if (localizedPublicationName == null && doc.RootElement.TryGetProperty("parentPubName", out var parentPubNameElement))
                {
                    var rawName = parentPubNameElement.GetString();
                    // Decode HTML entities like &nbsp; to proper characters and replace non-breaking spaces with regular spaces
                    localizedPublicationName = rawName != null ? WebUtility.HtmlDecode(rawName).Replace('\u00A0', ' ') : null;
                }

                ProcessSectionFiles(sectionFiles, doc.RootElement, sectionNumberSectionMap, sectionNumberTrackMap, ref sectionNumber, languageCode, sectionName);

                // Advance to next section after successful processing
                AdvanceToNextSection(ref sectionNumber, ref harvestLink, publicationCode, languageCode);
            }
            catch (Exception ex)
            {
                // Log and advance to next section on any exception during processing
                // This ensures we don't get stuck in an infinite loop
                Logger.Warning(ex, "Error processing section files for section {SectionNumber}. Advancing to next section.", sectionNumber);
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
            // Save to database via persister if available, otherwise save to files
            if (dataPersister != null)
            {
                // Use the publication name we extracted from the first section's API response (parentPubName)
                // Fallback to the mapping name if API didn't provide it
                var pubName = localizedPublicationName ?? publicationName;
                await dataPersister.SaveBiblePublicationSections(languageCode, publicationCode, pubName, sectionNumberSectionMap, sectionNumberTrackMap);
            }
            else
            {
                SaveSectionsAndTracks(sectionsDirectory, sectionsIndex, sectionNumberSectionMap, sectionNumberTrackMap);
            }

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
            // Decode HTML entities like &nbsp; to proper characters and replace non-breaking spaces with regular spaces
            title = rawTitle != null ? WebUtility.HtmlDecode(rawTitle).Replace('\u00A0', ' ') : string.Empty;
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
                // Decode HTML entities like &nbsp; to proper characters and replace non-breaking spaces with regular spaces
                sectionName = rawName != null ? WebUtility.HtmlDecode(rawName).Replace('\u00A0', ' ') : null;
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
                    // Decode HTML entities and replace non-breaking spaces with regular spaces, then extract section name
                    var decodedTitle = rawTitle != null ? WebUtility.HtmlDecode(rawTitle).Replace('\u00A0', ' ') : null;
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
        harvestLink = $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?output=json&pub={publicationCode}&booknum={sectionNumber}&fileformat=MP3&alllangs=0&langwritten={languageCode}";
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
