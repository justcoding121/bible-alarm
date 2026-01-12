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
using Bible.Alarm.AudioLinksHarvestor.Models.Video;
using Bible.Alarm.AudioLinksHarvestor.Utility;
using Bible.Alarm.Shared.Constants;
using Serilog;

namespace Bible.Alarm.AudioLinksHarvestor.Harvestors.Video;

internal class VideoHarvester(ILogger logger, DownloadUtility downloadUtility)
{
    private const int MaxConcurrentLanguageDownloads = 8;
    private const string PreferredQuality = "240p"; // Use lowest quality for audio-only playback

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

    private static readonly HashSet<string> TestRunLanguageCodes = ["E", "MY", "A"]; // A = Arabic, not AR (Bambara)

    internal async Task HarvestVideoLinks(bool isTestRun = false)
    {
        var languageCodeToInfo = new Dictionary<string, LanguageInfo>(StringComparer.OrdinalIgnoreCase);
        var languageCodeToPublications = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var publication in VideoPublicationCodeToNameMappings)
        {
            var languageEntries = await GetLanguageEntries(publication.Key, publication.Value, isTestRun);
            if (languageEntries == null || languageEntries.Count == 0)
            {
                continue;
            }

            await ProcessLanguageEntries(
                languageEntries,
                publication.Key,
                publication.Value,
                languageCodeToInfo,
                languageCodeToPublications);
        }

        SaveVideoMetadata(languageCodeToPublications, languageCodeToInfo);
    }

    private async Task<List<(string Code, string Name, string Direction)>?> GetLanguageEntries(
        string publicationCode,
        string publicationName,
        bool isTestRun)
    {
        string jsonString;
        try
        {
            // Use track=1 to get all available languages
            var harvestLink = $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?output=json&pub={publicationCode}&fileformat=MP4&alllangs=1&track=1&langwritten=E&txtCMSLang=E";
            jsonString = await downloadUtility.GetAsync(harvestLink);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("Response status code"))
        {
            return null;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to fetch languages for video publication {PublicationCode} ({PublicationName}). Skipping.",
                publicationCode, publicationName);
            return null;
        }

        var languageEntries = ParseLanguageEntries(jsonString);
        if (languageEntries == null || languageEntries.Count == 0)
        {
            return null;
        }

        return FilterLanguageEntriesForTestRun(languageEntries, isTestRun);
    }

    private static List<(string Code, string Name, string Direction)>? ParseLanguageEntries(string jsonString)
    {
        using var doc = JsonDocument.Parse(jsonString);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("languages", out var languages))
        {
            return null;
        }

        var languageEntries = new List<(string Code, string Name, string Direction)>();
        foreach (var item in languages.EnumerateObject())
        {
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

            // Normalize language code to uppercase for consistent storage
            languageEntries.Add((item.Name.ToUpperInvariant(), language, direction));
        }

        return languageEntries;
    }

    private static List<(string Code, string Name, string Direction)>? FilterLanguageEntriesForTestRun(
        List<(string Code, string Name, string Direction)> languageEntries,
        bool isTestRun)
    {
        if (!isTestRun)
        {
            return languageEntries;
        }

        var testRunEntries = languageEntries.Where(e => TestRunLanguageCodes.Contains(e.Code)).ToList();
        return testRunEntries.Count > 0 ? testRunEntries : null;
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
        logger.Information("Harvesting Video episode links for {PublicationName} in {Language} language.",
            publicationName, language);

        try
        {
            var success = await HarvestVideoEpisodes(publicationCode, languageCode);
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
            logger.Error(e, "Failed: Harvesting Video episode links for {PublicationName} in {Language} language.",
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
            var jsonString = await downloadUtility.GetAsync(categoryUrl);

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
            logger.Warning(ex, "Failed to fetch localized name for {PublicationCode} in {LanguageCode}",
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

    private async Task<bool> HarvestVideoEpisodes(string publicationCode, string languageCode)
    {
        var dir = $"{DirectoryHelper.IndexDirectory}/media/Video/{languageCode}/{publicationCode}";
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

        SaveEpisodes(dir, file, episodes);
        return true;
    }

    private async Task<VideoEpisode?> FetchEpisode(string publicationCode, string languageCode, int episodeNumber)
    {
        string jsonString;
        try
        {
            var harvestLink = $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?output=json&pub={publicationCode}&fileformat=MP4&langwritten={languageCode}&track={episodeNumber}";
            jsonString = await downloadUtility.GetAsync(harvestLink);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("Response status code"))
        {
            return null;
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to fetch episode {EpisodeNumber} for {PublicationCode} in {LanguageCode}",
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

    private void SaveVideoMetadata(
        Dictionary<string, List<string>> languageCodeToPublications,
        Dictionary<string, LanguageInfo> languageCodeToInfo)
    {
        var videoDir = $"{DirectoryHelper.IndexDirectory}/media/Video";
        if (!Directory.Exists(videoDir))
        {
            Directory.CreateDirectory(videoDir);
        }

        foreach (var languagePublication in languageCodeToPublications)
        {
            var languageCode = languagePublication.Key;
            var languageDir = $"{videoDir}/{languageCode}";
            if (!Directory.Exists(languageDir))
            {
                Directory.CreateDirectory(languageDir);
            }

            var publicationsJson = JsonSerializer.Serialize(
                languagePublication.Value.Select(pubCode =>
                {
                    // Use localized name if available, otherwise fall back to English
                    var name = localizedPublicationNames.TryGetValue((languageCode, pubCode), out var localizedName)
                        ? localizedName
                        : VideoPublicationCodeToNameMappings.GetValueOrDefault(pubCode, pubCode);

                    return new Publication
                    {
                        Code = pubCode,
                        Name = name
                    };
                }).OrderBy(x => x.Code));

            File.WriteAllText($"{languageDir}/publications.json", publicationsJson);
        }

        var languagesJson = JsonSerializer.Serialize(
            languageCodeToPublications.Select(x =>
            {
                var info = languageCodeToInfo[x.Key];
                return new Language
                {
                    Code = x.Key,
                    Name = info.Name,
                    Direction = info.Direction
                };
            }).OrderBy(x => x.Code));

        File.WriteAllText($"{videoDir}/languages.json", languagesJson);
    }
}
