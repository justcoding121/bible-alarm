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
using Bible.Alarm.AudioLinksHarvestor.Models.Music;
using Bible.Alarm.AudioLinksHarvestor.Utility;
using Bible.Alarm.Shared.Constants;
using Serilog;

namespace Bible.Alarm.AudioLinksHarvestor.Harvestors.Music;

internal class MusicHarvester(ILogger logger, DownloadUtility downloadUtility)
{
    private const int MaxConcurrentLanguageDownloads = 8;

    /// <summary>
    /// Default English names for vocal music publications (fallback if API doesn't return localized name)
    /// </summary>
    private static readonly Dictionary<string, string> vocalsPublicationCodeToNameMappings = new([
        new KeyValuePair<string, string>("osg","Original Songs"),
        new KeyValuePair<string, string>("sjjc","\"Sing Out Joyfully\" to Jehovah (2016)"),
        new KeyValuePair<string, string>("sjji","\"Sing Out Joyfully\" to Jehovah—Instrumental"),
        new KeyValuePair<string, string>("snv","Sing to Jehovah (2014) "),
        new KeyValuePair<string, string>("pksjj","Children's Songs")
    ]);

    /// <summary>
    /// Localized publication names: (languageCode, publicationCode) -> localizedName
    /// </summary>
    private readonly Dictionary<(string LanguageCode, string PublicationCode), string> localizedVocalNames = new();

    internal async Task HarvestVocalMusicLinks(bool isTestRun = false)
    {
        var languageCodeToInfo = new Dictionary<string, LanguageInfo>();
        var languageCodeToPublications = new Dictionary<string, List<string>>();

        foreach (var publication in vocalsPublicationCodeToNameMappings)
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

        SaveVocalMusicMetadata(languageCodeToPublications, languageCodeToInfo);
    }

    private async Task<List<(string Code, string Name, string Direction)>?> GetLanguageEntries(string publicationCode, string publicationName, bool isTestRun)
    {
        string jsonString;
        try
        {
            var harvestLink = $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?booknum=0&output=json&pub={publicationCode}&fileformat=MP3&alllangs=1&langwritten=E&txtCMSLang=E";
            jsonString = await downloadUtility.GetAsync(harvestLink);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("Response status code"))
        {
            return null;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to fetch languages for publication {PublicationCode} ({PublicationName}). Skipping.", publicationCode, publicationName);
            return null;
        }

        var languageEntries = ParseLanguageEntries(jsonString);
        if (languageEntries == null || languageEntries.Count == 0)
        {
            return null;
        }

        return FilterLanguageEntriesForTestRun(languageEntries, publicationCode, isTestRun);
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

            var language = nameElement.GetString();
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

    private static readonly HashSet<string> TestRunLanguageCodes = ["E", "MY"];

    private static List<(string Code, string Name, string Direction)>? FilterLanguageEntriesForTestRun(
        List<(string Code, string Name, string Direction)> languageEntries,
        string publicationCode,
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
        logger.Information("Harvesting Music track links for {PublicationName} of {Language} language.", publicationName, language);

        try
        {
            await HarvestMusicLinks(publicationCode, [publicationCode], languageCode);
            languageCodeToInfo[languageCode] = new LanguageInfo(language, direction);
            AddPublicationToLanguage(languageCode, publicationCode, languageCodeToPublications);
        }
        catch (Exception e)
        {
            logger.Error(e, "Failed: Harvesting Music track links for {PublicationName} of {Language} language.", publicationName, language);
        }
    }

    private static void AddPublicationToLanguage(
        string languageCode,
        string publicationCode,
        Dictionary<string, List<string>> languageCodeToPublications)
    {
        lock (languageCodeToPublications)
        {
            if (languageCodeToPublications.ContainsKey(languageCode))
            {
                // Only add if not already present (prevent duplicates)
                if (!languageCodeToPublications[languageCode].Contains(publicationCode))
                {
                    languageCodeToPublications[languageCode].Add(publicationCode);
                }
            }
            else
            {
                languageCodeToPublications[languageCode] = new List<string>([publicationCode]);
            }
        }
    }

    private void SaveVocalMusicMetadata(
        Dictionary<string, List<string>> languageCodeToPublications,
        Dictionary<string, LanguageInfo> languageCodeToInfo)
    {
        foreach (var languagePublication in languageCodeToPublications)
        {
            var languageCode = languagePublication.Key;
            var languageDir = $"{DirectoryHelper.IndexDirectory}/media/Music/Vocals/{languageCode}";
            if (!Directory.Exists(languageDir))
            {
                Directory.CreateDirectory(languageDir);
            }

            var publicationsJson = JsonSerializer.Serialize(
                languagePublication.Value.Select(publicationCode =>
                {
                    // Use localized publication name if available, otherwise fall back to English
                    var name = localizedVocalNames.TryGetValue((languageCode, publicationCode), out var localizedName)
                        ? localizedName
                        : vocalsPublicationCodeToNameMappings[publicationCode];

                    return new Publication
                    {
                        Code = publicationCode,
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

        File.WriteAllText($"{DirectoryHelper.IndexDirectory}/media/Music/Vocals/languages.json", languagesJson);
    }

    private static Dictionary<string, string> melodyPublicationCodeToNameMappings = new([
        new KeyValuePair<string, string>("iam","Sing Praises to Jehovah (1984)")
    ]);

    internal async Task HarvestMusicMelodyLinks(bool isTestRun = false)
    {
        var discs = new List<string>();
        var downloadCodes = new List<string>();

        foreach (var publication in melodyPublicationCodeToNameMappings)
        {
            downloadCodes.Clear();
            if (publication.Key == "iam")
            {
                for (var i = 1; i <= 9; i++)
                {
                    if (i is 7 or 8)
                    {
                        continue;
                    }

                    downloadCodes.Add($"{publication.Key}-{i}");
                }
            }
            else
            {
                downloadCodes.Add(publication.Key);
            }

            logger.Information("Harvesting Music track links for {PublicationName}.", publication.Value);
            await HarvestMusicLinks(publication.Key, downloadCodes);
        }

        File.WriteAllText($"{DirectoryHelper.IndexDirectory}/media/Music/Melodies/publications.json", JsonSerializer.Serialize(
        melodyPublicationCodeToNameMappings.Select(x => new
        Publication
        {
            Code = x.Key,
            Name = x.Value
        }).OrderBy(x => x.Code)));
    }

    private async Task<bool> HarvestMusicLinks(string publicationCode, List<string> publicationDownloadCodes, string? languageCode = null)
    {
        var dir = GetMusicDirectory(publicationCode, languageCode);
        var file = $"{dir}/tracks.json";

        var trackNumber = 1;
        var musicTracks = new List<MusicTrack>();
        string? localizedPubName = null;

        foreach (var publicationDownloadCode in publicationDownloadCodes)
        {
            var result = await FetchAndProcessMusicFiles(publicationDownloadCode, publicationCode, languageCode, trackNumber, musicTracks);
            trackNumber = result.TrackNumber;

            // Capture localized publication name (only need it once per publication/language combo)
            if (localizedPubName == null && result.LocalizedPubName != null)
            {
                localizedPubName = result.LocalizedPubName;
            }
        }

        if (musicTracks.Count == 0)
        {
            return false;
        }

        // Store localized publication name for vocal music
        if (languageCode != null && !string.IsNullOrEmpty(localizedPubName))
        {
            lock (localizedVocalNames)
            {
                localizedVocalNames[(languageCode, publicationCode)] = localizedPubName;
            }
        }

        SaveMusicTracks(dir, file, musicTracks);
        return true;
    }

    private static string GetMusicDirectory(string publicationCode, string? languageCode)
    {
        return languageCode == null
            ? $"{DirectoryHelper.IndexDirectory}/media/Music/Melodies/{publicationCode}"
            : $"{DirectoryHelper.IndexDirectory}/media/Music/Vocals/{languageCode}/{publicationCode}";
    }

    private async Task<(int TrackNumber, string? LocalizedPubName)> FetchAndProcessMusicFiles(
        string publicationDownloadCode,
        string publicationCode,
        string? languageCode,
        int trackNumber,
        List<MusicTrack> musicTracks)
    {
        string jsonString;
        try
        {
            var harvestLink = BuildMusicHarvestLink(publicationDownloadCode, languageCode);
            jsonString = await downloadUtility.GetAsync(harvestLink);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("Response status code"))
        {
            return (trackNumber, null);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to fetch tracks for publication {PublicationCode}. Skipping.", publicationDownloadCode);
            return (trackNumber, null);
        }

        // Parse and process within the same scope to keep JsonDocument alive
        using var doc = JsonDocument.Parse(jsonString);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("files", out var filesElement))
        {
            return (trackNumber, null);
        }

        var lc = languageCode ?? "E";
        if (!filesElement.TryGetProperty(lc, out var languageFiles) ||
            !languageFiles.TryGetProperty("MP3", out var musicFiles))
        {
            return (trackNumber, null);
        }

        // Extract localized publication name from pubName field
        string? localizedPubName = null;
        if (root.TryGetProperty("pubName", out var pubNameElement))
        {
            localizedPubName = pubNameElement.GetString();
        }

        var newTrackNumber = ProcessMusicFiles(musicFiles, publicationDownloadCode, languageCode, trackNumber, musicTracks);
        return (newTrackNumber, localizedPubName);
    }

    private static string BuildMusicHarvestLink(string publicationDownloadCode, string? languageCode)
    {
        var langParam = languageCode == null ? "&langwritten=E" : $"&langwritten={languageCode}";
        // Use txtCMSLang={languageCode} to get localized publication names and track titles
        var cmsLang = languageCode ?? "E";
        return $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?output=json&pub={publicationDownloadCode}&fileformat=MP3&alllangs=0{langParam}&txtCMSLang={cmsLang}";
    }

    private static int ProcessMusicFiles(
        JsonElement musicFiles,
        string publicationDownloadCode,
        string? languageCode,
        int trackNumber,
        List<MusicTrack> musicTracks)
    {
        foreach (var musicFile in musicFiles.EnumerateArray())
        {
            if (!TryExtractMusicTrackData(musicFile, out var url, out var track, out var title))
            {
                continue;
            }

            if (track == 0 || url.EndsWith(".zip") || ShouldSkipTrack(title))
            {
                continue;
            }

            var musicTrack = CreateMusicTrack(trackNumber, track, title, url, publicationDownloadCode, languageCode);
            musicTracks.Add(musicTrack);
            trackNumber++;
        }

        return trackNumber;
    }

    private static bool TryExtractMusicTrackData(JsonElement musicFile, out string url, out int track, out string title)
    {
        url = string.Empty;
        track = 0;
        title = "Unknown";

        if (!musicFile.TryGetProperty("file", out var fileElement) ||
            !fileElement.TryGetProperty("url", out var urlElement))
        {
            return false;
        }

        url = urlElement.GetString() ?? string.Empty;
        if (string.IsNullOrEmpty(url))
        {
            return false;
        }

        if (!musicFile.TryGetProperty("track", out var trackElement))
        {
            return false;
        }

        track = trackElement.GetInt32();

        if (musicFile.TryGetProperty("title", out var titleElement))
        {
            title = titleElement.ValueKind != JsonValueKind.Undefined ? titleElement.GetString()! : "Unknown";
        }

        return true;
    }

    private static bool ShouldSkipTrack(string title)
    {
        return title.Contains("audio descriptions", StringComparison.OrdinalIgnoreCase);
    }

    private static MusicTrack CreateMusicTrack(
        int trackNumber,
        int track,
        string title,
        string url,
        string publicationDownloadCode,
        string? languageCode)
    {
        // LookUpPath is no longer stored in the database - it's computed at runtime
        // The parameters are still passed in case we need them for other purposes
        _ = publicationDownloadCode;
        _ = languageCode;

        return new MusicTrack
        {
            Number = trackNumber,
            Title = title,
            Url = url
        };
    }

    private static void SaveMusicTracks(string dir, string file, List<MusicTrack> musicTracks)
    {
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var tracksJson = JsonSerializer.Serialize(musicTracks.OrderBy(x => x.Number));
        File.WriteAllText(file, tracksJson);
    }

}
