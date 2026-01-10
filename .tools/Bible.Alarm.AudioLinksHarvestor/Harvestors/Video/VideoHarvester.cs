#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

    private static readonly HashSet<string> TestRunLanguageCodes = ["E", "MY"];

    internal async Task HarvestVideoLinks(bool isTestRun = false)
    {
        var languageCodeToNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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
                languageCodeToNames,
                languageCodeToPublications);
        }

        SaveVideoMetadata(languageCodeToPublications, languageCodeToNames);
    }

    private async Task<List<(string Code, string Name)>?> GetLanguageEntries(
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

    private static List<(string Code, string Name)>? ParseLanguageEntries(string jsonString)
    {
        using var doc = JsonDocument.Parse(jsonString);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("languages", out var languages))
        {
            return null;
        }

        var languageEntries = new List<(string Code, string Name)>();
        foreach (var item in languages.EnumerateObject())
        {
            if (!item.Value.TryGetProperty("name", out var nameElement))
            {
                continue;
            }

            var language = nameElement.GetString();
            if (string.IsNullOrEmpty(language))
            {
                continue;
            }

            // Normalize language code to uppercase for consistent storage
            languageEntries.Add((item.Name.ToUpperInvariant(), language));
        }

        return languageEntries;
    }

    private static List<(string Code, string Name)>? FilterLanguageEntriesForTestRun(
        List<(string Code, string Name)> languageEntries,
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
        List<(string Code, string Name)> languageEntries,
        string publicationCode,
        string publicationName,
        Dictionary<string, string> languageCodeToNames,
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
                    languageCodeToNames,
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
        (string Code, string Name) entry,
        string publicationCode,
        string publicationName,
        Dictionary<string, string> languageCodeToNames,
        Dictionary<string, List<string>> languageCodeToPublications)
    {
        var (languageCode, language) = entry;
        logger.Information("Harvesting Video episode links for {PublicationName} in {Language} language.",
            publicationName, language);

        try
        {
            var success = await HarvestVideoEpisodes(publicationCode, languageCode);
            if (success)
            {
                lock (languageCodeToNames)
                {
                    languageCodeToNames[languageCode] = language;
                }
                AddPublicationToLanguage(languageCode, publicationCode, languageCodeToPublications);
            }
        }
        catch (Exception e)
        {
            logger.Error(e, "Failed: Harvesting Video episode links for {PublicationName} in {Language} language.",
                publicationName, language);
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
            title = titleElement.GetString() ?? "Unknown";
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
        Dictionary<string, string> languageCodeToNames)
    {
        var videoDir = $"{DirectoryHelper.IndexDirectory}/media/Video";
        if (!Directory.Exists(videoDir))
        {
            Directory.CreateDirectory(videoDir);
        }

        foreach (var languagePublication in languageCodeToPublications)
        {
            var languageDir = $"{videoDir}/{languagePublication.Key}";
            if (!Directory.Exists(languageDir))
            {
                Directory.CreateDirectory(languageDir);
            }

            var publicationsJson = JsonSerializer.Serialize(
                languagePublication.Value.Select(x => new Publication
                {
                    Code = x,
                    Name = VideoPublicationCodeToNameMappings[x]
                }).OrderBy(x => x.Code));

            File.WriteAllText($"{languageDir}/publications.json", publicationsJson);
        }

        var languagesJson = JsonSerializer.Serialize(
            languageCodeToPublications.Select(x => new Language
            {
                Code = x.Key,
                Name = languageCodeToNames[x.Key]
            }).OrderBy(x => x.Code));

        File.WriteAllText($"{videoDir}/languages.json", languagesJson);
    }
}
