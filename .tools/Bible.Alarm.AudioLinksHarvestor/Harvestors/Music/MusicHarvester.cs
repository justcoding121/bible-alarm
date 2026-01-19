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
using Bible.Alarm.AudioLinksHarvestor.Models.Music;
using Bible.Alarm.AudioLinksHarvestor.Utility;
using Bible.Alarm.Shared.Constants;
using Serilog;

namespace Bible.Alarm.AudioLinksHarvestor.Harvestors.Music;

internal class MusicHarvester : BaseHarvester
{
    private readonly IDataPersister? dataPersister;

    public MusicHarvester(ILogger logger, DownloadUtility downloadUtility, IDataPersister? dataPersister = null)
        : base(logger, downloadUtility)
    {
        this.dataPersister = dataPersister;
    }

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
            Logger.Information("Starting harvest for Vocal Music publication: {PublicationName} ({PublicationCode})", 
                publication.Value, publication.Key);
            
            var languageEntries = await GetLanguageEntries(publication.Key, publication.Value, isTestRun);
            if (languageEntries == null || languageEntries.Count == 0)
            {
                Logger.Warning("No languages found for Vocal Music publication: {PublicationName} ({PublicationCode})", 
                    publication.Value, publication.Key);
                continue;
            }

            Logger.Information("Found {Count} language(s) for Vocal Music publication: {PublicationName} ({PublicationCode})", 
                languageEntries.Count, publication.Value, publication.Key);

            await ProcessLanguageEntries(
                languageEntries,
                publication.Key,
                publication.Value,
                languageCodeToInfo,
                languageCodeToPublications);
        }
    }

    private async Task<List<(string Code, string Name, string Direction)>?> GetLanguageEntries(string publicationCode, string publicationName, bool isTestRun)
    {
        string jsonString;
        try
        {
            var harvestLink = $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?booknum=0&output=json&pub={publicationCode}&fileformat=MP3&alllangs=1&langwritten=E";
            jsonString = await DownloadUtility.GetAsync(harvestLink);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("Response status code"))
        {
            return null;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to fetch languages for publication {PublicationCode} ({PublicationName}). Skipping.", publicationCode, publicationName);
            return null;
        }

        var languageEntries = ParseLanguageEntries(jsonString);
        if (languageEntries == null || languageEntries.Count == 0)
        {
            return null;
        }

        return FilterLanguageEntriesForTestRun(languageEntries, isTestRun);
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
        Logger.Information("Harvesting Music track links for {PublicationName} of {Language} language.", publicationName, language);

        try
        {
            await HarvestMusicLinks(publicationCode, [publicationCode], languageCode);
            languageCodeToInfo[languageCode] = new LanguageInfo(language, direction);
            AddPublicationToLanguage(languageCode, publicationCode, languageCodeToPublications);
        }
        catch (Exception e)
        {
            Logger.Error(e, "Failed: Harvesting Music track links for {PublicationName} of {Language} language.", publicationName, language);
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


    private static Dictionary<string, string> melodyPublicationCodeToNameMappings = new([
        new KeyValuePair<string, string>("iam","Kingdom Melodies")
    ]);

    internal async Task HarvestMusicMelodyLinks(bool isTestRun = false)
    {
        var discs = new List<string>();
        var downloadCodes = new List<string>();

        foreach (var publication in melodyPublicationCodeToNameMappings)
        {
            Logger.Information("Starting harvest for Instrumental Music publication: {PublicationName} ({PublicationCode})", 
                publication.Value, publication.Key);
            
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

            Logger.Information("Harvesting Music track links for {PublicationName}.", publication.Value);
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

        // For iam (Kingdom Melodies), save tracks grouped by disc
        if (publicationCode == "iam" && languageCode == null)
        {
            var discTracksMap = new Dictionary<string, List<MusicTrack>>();
            var discNamesMap = new Dictionary<string, string>();

            foreach (var publicationDownloadCode in publicationDownloadCodes)
            {
                var discTracks = new List<MusicTrack>();
                var result = await FetchAndProcessMusicFiles(publicationDownloadCode, publicationCode, languageCode, 1, discTracks);
                
                if (discTracks.Count > 0)
                {
                    discTracksMap[publicationDownloadCode] = discTracks;
                    
                    // Store disc name if available
                    if (result.DiscName != null)
                    {
                        discNamesMap[publicationDownloadCode] = result.DiscName;
                    }
                }
            }

            if (discTracksMap.Count == 0)
            {
                return false;
            }

            // Save to database via persister if available, otherwise save to files
            if (dataPersister != null)
            {
                await dataPersister.SaveMelodyMusicTracks(publicationCode, discTracksMap, discNamesMap);
            }
            else
            {
                // Save each disc's tracks separately
                foreach (var disc in discTracksMap)
                {
                    var discDir = $"{dir}/{disc.Key}";
                    var discFile = $"{discDir}/tracks.json";
                    SaveMusicTracks(discDir, discFile, disc.Value);
                    
                    // Save disc info (name) if available
                    if (discNamesMap.TryGetValue(disc.Key, out var discName))
                    {
                        var discInfoFile = $"{discDir}/disc.json";
                        var discInfo = new { Code = disc.Key, Name = discName };
                        File.WriteAllText(discInfoFile, JsonSerializer.Serialize(discInfo));
                    }
                }

                // Also save a main tracks.json with all tracks for backward compatibility
                var allTracks = discTracksMap.Values.SelectMany(t => t).OrderBy(t => t.Number).ToList();
                SaveMusicTracks(dir, $"{dir}/tracks.json", allTracks);
            }
            
            return true;
        }
        else
        {
            // Original logic for other publications
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

            // Save to database via persister if available, otherwise save to files
            if (dataPersister != null)
            {
                await dataPersister.SaveMusicTracks(publicationCode, languageCode, musicTracks);
            }
            else
            {
                SaveMusicTracks(dir, file, musicTracks);
            }
            return true;
        }
    }

    private static string GetMusicDirectory(string publicationCode, string? languageCode)
    {
        // Unified structure: media/Music/{Vocals|Melodies}/{languageCode?}/{publicationCode}
        if (languageCode == null)
        {
            // Melodies: no language
            var normalizedPublicationCode = publicationCode.ToUpperInvariant();
            return $"{DirectoryHelper.IndexDirectory}/media/Music/Melodies/{normalizedPublicationCode}";
        }
        else
        {
            // Vocals: with language
            var normalizedLanguageCode = languageCode.ToUpperInvariant();
            var normalizedPublicationCode = publicationCode.ToUpperInvariant();
            return $"{DirectoryHelper.IndexDirectory}/media/Music/Vocals/{normalizedLanguageCode}/{normalizedPublicationCode}";
        }
    }

    private async Task<(int TrackNumber, string? LocalizedPubName, string? DiscName)> FetchAndProcessMusicFiles(
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
            jsonString = await DownloadUtility.GetAsync(harvestLink);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("Response status code"))
        {
            return (trackNumber, null, null);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to fetch tracks for publication {PublicationCode}. Skipping.", publicationDownloadCode);
            return (trackNumber, null, null);
        }

        // Parse and process within the same scope to keep JsonDocument alive
        using var doc = JsonDocument.Parse(jsonString);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("files", out var filesElement))
        {
            return (trackNumber, null, null);
        }

        var lc = languageCode ?? "E";
        if (!filesElement.TryGetProperty(lc, out var languageFiles) ||
            !languageFiles.TryGetProperty("MP3", out var musicFiles))
        {
            return (trackNumber, null, null);
        }

        // Extract localized publication name from pubName field
        string? localizedPubName = null;
        string? discName = null;
        if (root.TryGetProperty("pubName", out var pubNameElement))
        {
            var rawName = pubNameElement.GetString();
            // Decode HTML entities like &nbsp; to proper characters and replace non-breaking spaces with regular spaces
            var decodedName = rawName != null ? WebUtility.HtmlDecode(rawName).Replace('\u00A0', ' ') : null;
            
            // For iam (Kingdom Melodies), pubName is the disc name (e.g., "Kingdom Melodies, Volume 1")
            if (publicationCode == "iam" && languageCode == null)
            {
                discName = decodedName;
            }
            else
            {
                localizedPubName = decodedName;
            }
        }

        var newTrackNumber = ProcessMusicFiles(musicFiles, publicationDownloadCode, languageCode, trackNumber, musicTracks);
        return (newTrackNumber, localizedPubName, discName);
    }

    private static string BuildMusicHarvestLink(string publicationDownloadCode, string? languageCode)
    {
        var langParam = languageCode == null ? "&langwritten=E" : $"&langwritten={languageCode}";
        return $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?output=json&pub={publicationDownloadCode}&fileformat=MP3&alllangs=0{langParam}";
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
            var rawTitle = titleElement.ValueKind != JsonValueKind.Undefined ? titleElement.GetString() : null;
            // Decode HTML entities like &nbsp; to proper characters and replace non-breaking spaces with regular spaces
            title = rawTitle != null ? WebUtility.HtmlDecode(rawTitle).Replace('\u00A0', ' ') : "Unknown";
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
        // Store DownloadCode for melody music that uses disc codes (e.g., "iam-1", "iam-2")
        // Store OriginalTrackNumber for melody music - the API expects the track number within that disc
        return new MusicTrack
        {
            Number = trackNumber,
            Title = title,
            Url = url,
            DownloadCode = publicationDownloadCode,
            OriginalTrackNumber = track // Store the original track number from API (within the disc)
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
