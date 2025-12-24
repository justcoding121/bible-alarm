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

    private static Dictionary<string, string> vocalsPublicationCodeToNameMappings = new([
        new KeyValuePair<string, string>("osg","Original Songs"),
        new KeyValuePair<string, string>("sjjc","\"Sing Out Joyfully\" to Jehovah (2016)"),
        new KeyValuePair<string, string>("snv","Sing to Jehovah (2014) ")
    ]);

    internal async Task HarvestVocalMusicLinks(bool isTestRun = false)
    {
        var languageCodeToNames = new Dictionary<string, string>();
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
                languageCodeToNames,
                languageCodeToPublications);
        }

        SaveVocalMusicMetadata(languageCodeToPublications, languageCodeToNames);
    }

    private async Task<List<(string Code, string Name)>?> GetLanguageEntries(string publicationCode, string publicationName, bool isTestRun)
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

            languageEntries.Add((item.Name, language));
        }

        return languageEntries;
    }

    private static List<(string Code, string Name)>? FilterLanguageEntriesForTestRun(
        List<(string Code, string Name)> languageEntries,
        string publicationCode,
        bool isTestRun)
    {
        if (!isTestRun)
        {
            return languageEntries;
        }

        var englishEntry = languageEntries.FirstOrDefault(e => e.Code == "E");
        if (englishEntry.Code == "E")
        {
            return [englishEntry];
        }

        return null;
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
        logger.Information("Harvesting Music track links for {PublicationName} of {Language} language.", publicationName, language);

        try
        {
            await HarvestMusicLinks(publicationCode, [publicationCode], languageCode);
            languageCodeToNames[languageCode] = language;
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
                languageCodeToPublications[languageCode].Add(publicationCode);
            }
            else
            {
                languageCodeToPublications[languageCode] = new List<string>([publicationCode]);
            }
        }
    }

    private void SaveVocalMusicMetadata(
        Dictionary<string, List<string>> languageCodeToPublications,
        Dictionary<string, string> languageCodeToNames)
    {
        foreach (var languagePublication in languageCodeToPublications)
        {
            var languageDir = $"{DirectoryHelper.IndexDirectory}/media/Music/Vocals/{languagePublication.Key}";
            if (!Directory.Exists(languageDir))
            {
                Directory.CreateDirectory(languageDir);
            }

            var publicationsJson = JsonSerializer.Serialize(
                languagePublication.Value.Select(x => new Publication
                {
                    Code = x,
                    Name = vocalsPublicationCodeToNameMappings[x]
                }).OrderBy(x => x.Code));

            File.WriteAllText($"{languageDir}/publications.json", publicationsJson);
        }

        var languagesJson = JsonSerializer.Serialize(
            languageCodeToPublications.Select(x => new Language
            {
                Code = x.Key,
                Name = languageCodeToNames[x.Key]
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

    private async Task<bool> HarvestMusicLinks(string publicationCode, List<string> publicationDownloadCodes, string languageCode = null)
    {
        var dir = GetMusicDirectory(publicationCode, languageCode);
        var file = $"{dir}/tracks.json";

        var trackNumber = 1;
        var musicTracks = new List<MusicTrack>();

        foreach (var publicationDownloadCode in publicationDownloadCodes)
        {
            var musicFiles = await FetchAndParseMusicFiles(publicationDownloadCode, languageCode);
            if (!musicFiles.HasValue)
            {
                continue;
            }

            ProcessMusicFiles(musicFiles.Value, publicationDownloadCode, languageCode, ref trackNumber, musicTracks);
        }

        if (musicTracks.Count == 0)
        {
            return false;
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

    private async Task<JsonElement?> FetchAndParseMusicFiles(string publicationDownloadCode, string? languageCode)
    {
        string jsonString;
        try
        {
            var harvestLink = BuildMusicHarvestLink(publicationDownloadCode, languageCode);
            jsonString = await downloadUtility.GetAsync(harvestLink);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("Response status code"))
        {
            return null;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to fetch tracks for publication {PublicationCode}. Skipping.", publicationDownloadCode);
            return null;
        }

        return ParseMusicFilesFromJson(jsonString, languageCode);
    }

    private static string BuildMusicHarvestLink(string publicationDownloadCode, string? languageCode)
    {
        var langParam = languageCode == null ? "&langwritten=E" : $"&langwritten={languageCode}";
        return $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?output=json&pub={publicationDownloadCode}&fileformat=MP3&alllangs=0{langParam}&txtCMSLang=E";
    }

    private static JsonElement? ParseMusicFilesFromJson(string jsonString, string? languageCode)
    {
        using var doc = JsonDocument.Parse(jsonString);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("files", out var filesElement))
        {
            return null;
        }

        var lc = languageCode ?? "E";
        if (!filesElement.TryGetProperty(lc, out var languageFiles) ||
            !languageFiles.TryGetProperty("MP3", out var musicFiles))
        {
            return null;
        }

        return musicFiles;
    }

    private static void ProcessMusicFiles(
        JsonElement musicFiles,
        string publicationDownloadCode,
        string? languageCode,
        ref int trackNumber,
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
        var langParam = languageCode == null ? "&langwritten=E" : $"&langwritten={languageCode}";
        var lookUpPath = $"?output=json&pub={publicationDownloadCode}&fileformat=MP3{langParam}&txtCMSLang=E&track={track}";

        return new MusicTrack
        {
            Number = trackNumber,
            Title = title,
            Url = url,
            LookUpPath = lookUpPath
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
