#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.AudioLinksHarvestor.Models;
using Bible.Alarm.AudioLinksHarvestor.Utility;
using Bible.Alarm.Shared.Constants;
using SharedHelpers = Bible.Alarm.Shared.Helpers;
using Serilog;

namespace Bible.Alarm.AudioLinksHarvestor.Harvestors;

internal class VideoHarvester : BaseHarvester
{
    private const string PreferredQuality = "240p"; // Use lowest quality for audio-only playback
    private readonly IDataPersister? dataPersister;
    private readonly SignLanguageChecker signLanguageChecker;

    public VideoHarvester(ILogger logger, DownloadUtility downloadUtility, IDataPersister? dataPersister = null)
        : base(logger, downloadUtility)
    {
        this.dataPersister = dataPersister;
        signLanguageChecker = new SignLanguageChecker(logger, downloadUtility);
    }

    /// <summary>
    /// Video publication code to name mappings (for logging/fallback).
    /// Codes come from centralized JwSourceHelper.VideoPublicationCodes.
    /// </summary>
    private static readonly Dictionary<string, string> VideoPublicationCodeToNameMappings = new([
        new KeyValuePair<string, string>("gnj", "The Good News According to Jesus")
    ]);

    /// <summary>
    /// Maps publication codes to Mediator API category keys for fetching localized names.
    /// </summary>
    private static readonly Dictionary<string, string> PublicationCodeToCategoryKey = new([
        new KeyValuePair<string, string>("gnj", "DramasGoodNews")
    ]);

    /// <summary>
    /// Localized publication names: (languageCode, publicationCode) -> localizedName
    /// </summary>
    private readonly Dictionary<(string LanguageCode, string PublicationCode), string> localizedPublicationNames = new();
    private readonly object localizedNamesLock = new();


    internal async Task HarvestVideoLinks(bool isTestRun = false)
    {
        var languageCodeToInfo = new Dictionary<string, LanguageInfo>(StringComparer.OrdinalIgnoreCase);
        var languageCodeToPublications = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        // Use codes from centralized JwSourceHelper
        foreach (var publicationCode in SharedHelpers.JwSourceHelper.VideoPublicationCodes)
        {
            var publicationName = VideoPublicationCodeToNameMappings.GetValueOrDefault(publicationCode, publicationCode);
            Logger.Information("Starting harvest for Video publication: {PublicationName} ({PublicationCode})", 
                publicationName, publicationCode);
            
            var languageEntries = await GetLanguageEntries(publicationCode, publicationName, isTestRun);
            if (languageEntries == null || languageEntries.Count == 0)
            {
                Logger.Warning("No languages found for Video publication: {PublicationName} ({PublicationCode})", 
                    publicationName, publicationCode);
                continue;
            }

            // Filter out sign languages
            languageEntries = await signLanguageChecker.FilterSignLanguagesAsync(languageEntries);

            if (languageEntries.Count == 0)
            {
                Logger.Warning("No non-sign languages found for Video publication: {PublicationName} ({PublicationCode})", 
                    publicationName, publicationCode);
                continue;
            }

            // Save discovered languages for on-demand fetching (excluding English)
            // The alllangs=1 response already lists only available languages, so no verification needed
            if (dataPersister != null)
            {
                var discoveredLanguages = languageEntries
                    .Where(e => !e.Code.Equals("E", StringComparison.OrdinalIgnoreCase))
                    .ToDictionary(
                        e => e.Code,
                        e => new LanguageInfo(e.Name, e.Direction));
                
                if (discoveredLanguages.Count > 0)
                {
                    await dataPersister.SavePublicationLanguages(publicationCode, discoveredLanguages);
                }
            }

            // Verify English (E) is available (it will be seeded separately after discovery)
            var englishEntry = languageEntries.FirstOrDefault(e => e.Code.Equals("E", StringComparison.OrdinalIgnoreCase));
            if (englishEntry == default)
            {
                Logger.Warning("English (E) not found in discovered languages for publication {PublicationCode}. Skipping.", publicationCode);
                continue;
            }

            // Add English to language mappings (for reference, but don't process it here)
            languageCodeToInfo[englishEntry.Code] = new LanguageInfo(englishEntry.Name, englishEntry.Direction);
            AddPublicationToLanguage(englishEntry.Code, publicationCode, languageCodeToPublications);
        }

        // Series category flat video (same API shape: GETPUBMEDIALINKS pub=code track=N, MP4)
        foreach (var publicationCode in SharedHelpers.JwSourceHelper.SeriesPublicationCodes)
        {
            var publicationName = VideoPublicationCodeToNameMappings.GetValueOrDefault(publicationCode, publicationCode);
            Logger.Information("Starting harvest for Series publication: {PublicationName} ({PublicationCode})",
                publicationName, publicationCode);

            var languageEntries = await GetLanguageEntries(publicationCode, publicationName, isTestRun);
            if (languageEntries == null || languageEntries.Count == 0)
            {
                Logger.Warning("No languages found for Series publication: {PublicationName} ({PublicationCode})",
                    publicationName, publicationCode);
                continue;
            }

            languageEntries = await signLanguageChecker.FilterSignLanguagesAsync(languageEntries);

            if (languageEntries.Count == 0)
            {
                Logger.Warning("No non-sign languages found for Series publication: {PublicationName} ({PublicationCode})",
                    publicationName, publicationCode);
                continue;
            }

            if (dataPersister != null)
            {
                var discoveredLanguages = languageEntries
                    .Where(e => !e.Code.Equals("E", StringComparison.OrdinalIgnoreCase))
                    .ToDictionary(
                        e => e.Code,
                        e => new LanguageInfo(e.Name, e.Direction));

                if (discoveredLanguages.Count > 0)
                {
                    await dataPersister.SavePublicationLanguages(publicationCode, discoveredLanguages);
                }
            }

            var englishEntry = languageEntries.FirstOrDefault(e => e.Code.Equals("E", StringComparison.OrdinalIgnoreCase));
            if (englishEntry == default)
            {
                Logger.Warning("English (E) not found in discovered languages for publication {PublicationCode}. Skipping.", publicationCode);
                continue;
            }

            languageCodeToInfo[englishEntry.Code] = new LanguageInfo(englishEntry.Name, englishEntry.Direction);
            AddPublicationToLanguage(englishEntry.Code, publicationCode, languageCodeToPublications);
        }
    }

    private async Task<List<(string Code, string Name, string Direction)>?> GetLanguageEntries(
        string publicationCode,
        string publicationName,
        bool isTestRun)
    {
        Logger.Information("Fetching languages for Video publication: {PublicationName} ({PublicationCode})", 
            publicationName, publicationCode);
        
        string jsonString;
        try
        {
            // Use track=1 to get all available languages
            var harvestLink = $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?output=json&pub={publicationCode}&fileformat=MP4&alllangs=1&track=1&langwritten=E";
            jsonString = await DownloadUtility.GetAsync(harvestLink);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("Response status code"))
        {
            Logger.Warning("No languages found for Video publication: {PublicationName} ({PublicationCode})", 
                publicationName, publicationCode);
            return null;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to fetch languages for video publication {PublicationCode} ({PublicationName}). Skipping.",
                publicationCode, publicationName);
            return null;
        }

        var languageEntries = ParseLanguageEntries(jsonString);
        if (languageEntries == null || languageEntries.Count == 0)
        {
            Logger.Warning("No languages found for Video publication: {PublicationName} ({PublicationCode})", 
                publicationName, publicationCode);
            return null;
        }

        var filteredEntries = FilterLanguageEntriesForTestRun(languageEntries, isTestRun);
        if (filteredEntries != null && filteredEntries.Count > 0)
        {
            Logger.Information("Found {Count} language(s) for Video publication: {PublicationName} ({PublicationCode})", 
                filteredEntries.Count, publicationName, publicationCode);
        }
        
        return filteredEntries;
    }

    private async Task ProcessLanguageEntries(
        List<(string Code, string Name, string Direction)> languageEntries,
        string publicationCode,
        string publicationName,
        Dictionary<string, LanguageInfo> languageCodeToInfo,
        Dictionary<string, List<string>> languageCodeToPublications)
    {
        using var semaphore = new SemaphoreSlim(MaxConcurrentLanguageDownloads, MaxConcurrentLanguageDownloads);
        var languageTasks = languageEntries.Select(async entry =>
        {
            await semaphore.WaitAsync();
            try
            {
                await ProcessLanguageEntry(
                    entry,
                    publicationCode,
                    publicationName,
                    languageCodeToInfo,
                    languageCodeToPublications);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(languageTasks);
    }

    private async Task ProcessLanguageEntry(
        (string Code, string Name, string Direction) entry,
        string publicationCode,
        string publicationName,
        Dictionary<string, LanguageInfo> languageCodeToInfo,
        Dictionary<string, List<string>> languageCodeToPublications)
    {
        var (languageCode, language, direction) = entry;
        Logger.Information("Harvesting Video episode links for {PublicationName} in {Language} language.",
            publicationName, language);

        try
        {
            var success = await HarvestVideoEpisodes(publicationCode, languageCode, publicationName);
            if (success)
            {
                lock (languageCodeToInfo)
                {
                    languageCodeToInfo[languageCode] = new LanguageInfo(language, direction);
                }
                AddPublicationToLanguage(languageCode, publicationCode, languageCodeToPublications);

                // Fetch localized publication name from Mediator API
                await FetchLocalizedPublicationName(publicationCode, languageCode);
            }
        }
        catch (Exception e)
        {
            Logger.Error(e, "Failed: Harvesting Video episode links for {PublicationName} in {Language} language.",
                publicationName, language);
        }
    }

    private async Task FetchLocalizedPublicationName(string publicationCode, string languageCode)
    {
        if (!PublicationCodeToCategoryKey.TryGetValue(publicationCode, out var categoryKey))
        {
            return;
        }

        try
        {
            var categoryUrl = $"{AppConstants.ApiEndpoints.JwOrgMediatorApiBaseUrl}/categories/{languageCode}/{categoryKey}?detailed=1";
            var jsonString = await DownloadUtility.GetAsync(categoryUrl);

            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            if (root.TryGetProperty("category", out var category) &&
                category.TryGetProperty("name", out var nameElement))
            {
                var rawName = nameElement.GetString();
                // Decode HTML entities like &nbsp; to proper characters and replace non-breaking spaces with regular spaces
                var localizedName = rawName != null ? WebUtility.HtmlDecode(rawName).Replace('\u00A0', ' ') : null;
                if (!string.IsNullOrEmpty(localizedName))
                {
                    lock (localizedNamesLock)
                    {
                        localizedPublicationNames[(languageCode.ToUpperInvariant(), publicationCode)] = localizedName;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Failed to fetch localized name for {PublicationCode} in {LanguageCode}",
                publicationCode, languageCode);
        }
    }

    private static void AddPublicationToLanguage(
        string languageCode,
        string publicationCode,
        Dictionary<string, List<string>> languageCodeToPublications)
    {
        lock (languageCodeToPublications)
        {
            if (languageCodeToPublications.TryGetValue(languageCode, out var publications))
            {
                if (!publications.Contains(publicationCode))
                {
                    publications.Add(publicationCode);
                }
            }
            else
            {
                languageCodeToPublications[languageCode] = [publicationCode];
            }
        }
    }

    private async Task<bool> HarvestVideoEpisodes(string publicationCode, string languageCode, string publicationName)
    {
        // Unified structure: media/Dramas/{languageCode}/{publicationCode} (no Audio/Video prefix)
        // Videos use "Dramas" category, IsVideo flag in database determines if it's video or audio
        var normalizedLanguageCode = languageCode.ToUpperInvariant();
        var normalizedPublicationCode = publicationCode.ToUpperInvariant();
        var dir = $"{DirectoryHelper.IndexDirectory}/media/Dramas/{normalizedLanguageCode}/{normalizedPublicationCode}";
        var file = $"{dir}/episodes.json";

        var episodes = new List<VideoEpisode>();
        var episodeNumber = 1;
        var consecutiveFailures = 0;
        const int maxConsecutiveFailures = 3;

        while (consecutiveFailures < maxConsecutiveFailures)
        {
            var episode = await FetchEpisode(publicationCode, languageCode, episodeNumber);
            if (episode == null)
            {
                consecutiveFailures++;
                episodeNumber++;
                continue;
            }

            consecutiveFailures = 0;
            episodes.Add(episode);
            episodeNumber++;
        }

        if (episodes.Count == 0)
        {
            return false;
        }

        // Save to database via persister if available, otherwise save to files
        if (dataPersister != null)
        {
            await dataPersister.SaveVideoEpisodes(languageCode, publicationCode, publicationName, episodes);
        }
        else
        {
            SaveEpisodes(dir, file, episodes);
        }
        return true;
    }

    private async Task<VideoEpisode?> FetchEpisode(string publicationCode, string languageCode, int episodeNumber)
    {
        string jsonString;
        try
        {
            var harvestLink = $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?output=json&pub={publicationCode}&fileformat=MP4&langwritten={languageCode}&track={episodeNumber}";
            jsonString = await DownloadUtility.GetAsync(harvestLink);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("Response status code"))
        {
            return null;
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Failed to fetch episode {EpisodeNumber} for {PublicationCode} in {LanguageCode}",
                episodeNumber, publicationCode, languageCode);
            return null;
        }

        return ParseEpisode(jsonString, publicationCode, languageCode, episodeNumber);
    }

    private VideoEpisode? ParseEpisode(string jsonString, string publicationCode, string languageCode, int episodeNumber)
    {
        using var doc = JsonDocument.Parse(jsonString);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("files", out var filesElement))
        {
            return null;
        }

        if (!filesElement.TryGetProperty(languageCode, out var languageFiles))
        {
            // Try uppercase version
            if (!filesElement.TryGetProperty(languageCode.ToUpperInvariant(), out languageFiles))
            {
                return null;
            }
        }

        if (!languageFiles.TryGetProperty("MP4", out var mp4Files))
        {
            return null;
        }

        // Find the preferred quality (240p) or fallback to first available
        JsonElement? selectedFile = null;
        foreach (var file in mp4Files.EnumerateArray())
        {
            if (file.TryGetProperty("label", out var labelElement))
            {
                var label = labelElement.GetString();
                if (label == PreferredQuality)
                {
                    selectedFile = file;
                    break;
                }
            }

            // Keep first file as fallback
            selectedFile ??= file;
        }

        if (!selectedFile.HasValue)
        {
            return null;
        }

        var fileElement = selectedFile.Value;

        if (!fileElement.TryGetProperty("file", out var fileInfo) ||
            !fileInfo.TryGetProperty("url", out var urlElement))
        {
            return null;
        }

        var url = urlElement.GetString();
        if (string.IsNullOrEmpty(url))
        {
            return null;
        }

        var title = "Unknown";
        if (fileElement.TryGetProperty("title", out var titleElement))
        {
            var rawTitle = titleElement.GetString();
            // Decode HTML entities like &nbsp; to proper characters and replace non-breaking spaces with regular spaces
            title = rawTitle != null ? WebUtility.HtmlDecode(rawTitle).Replace('\u00A0', ' ') : "Unknown";
        }

        double duration = 0;
        if (fileElement.TryGetProperty("duration", out var durationElement))
        {
            duration = durationElement.GetDouble();
        }

        var lookUpPath = $"?output=json&pub={publicationCode}&fileformat=MP4&langwritten={languageCode}&track={episodeNumber}";

        return new VideoEpisode
        {
            Number = episodeNumber,
            Title = title,
            Url = url,
            LookUpPath = lookUpPath,
            Duration = duration
        };
    }

    private static void SaveEpisodes(string dir, string file, List<VideoEpisode> episodes)
    {
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var episodesJson = JsonSerializer.Serialize(episodes.OrderBy(x => x.Number));
        File.WriteAllText(file, episodesJson);
    }

}
