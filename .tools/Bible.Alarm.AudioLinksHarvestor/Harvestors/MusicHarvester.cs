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

internal class MusicHarvester : BaseHarvester
{
    private readonly IDataPersister? dataPersister;
    private readonly SignLanguageChecker signLanguageChecker;

    public MusicHarvester(ILogger logger, DownloadUtility downloadUtility, IDataPersister? dataPersister = null)
        : base(logger, downloadUtility)
    {
        this.dataPersister = dataPersister;
        signLanguageChecker = new SignLanguageChecker(logger, downloadUtility);
    }


    /// <summary>
    /// Localized publication names: (languageCode, publicationCode) -> localizedName
    /// </summary>
    private readonly Dictionary<(string LanguageCode, string PublicationCode), string> localizedVocalNames = new();

    internal async Task HarvestVocalMusicLinks(bool isTestRun = false)
    {
        var languageCodeToInfo = new Dictionary<string, LanguageInfo>();
        var languageCodeToPublications = new Dictionary<string, List<string>>();

        // List of vocal music publication codes to harvest (from centralized JwSourceHelper)
        var vocalMusicPublicationCodes = SharedHelpers.JwSourceHelper.VocalMusicPublicationCodes;

        foreach (var publicationCode in vocalMusicPublicationCodes)
        {
            Logger.Information("Starting harvest for Vocal Music publication: {PublicationCode}", publicationCode);
            
            var languageEntries = await GetLanguageEntries(publicationCode, publicationCode, isTestRun);
            if (languageEntries == null || languageEntries.Count == 0)
            {
                Logger.Warning("No languages found for Vocal Music publication: {PublicationCode}", publicationCode);
                continue;
            }

            Logger.Information("Found {Count} language(s) for Vocal Music publication: {PublicationCode}", 
                languageEntries.Count, publicationCode);

            // Filter out sign languages
            languageEntries = await signLanguageChecker.FilterSignLanguagesAsync(languageEntries);

            if (languageEntries.Count == 0)
            {
                Logger.Warning("No non-sign languages found for Vocal Music publication: {PublicationCode}", publicationCode);
                continue;
            }

            // Save discovered languages for on-demand fetching (excluding English)
            // The alllangs=1 response already lists only available languages, so no verification needed
            if (dataPersister != null)
            {
                var discoveredLanguages = languageEntries
                    .Where(e => !e.LanguageCode.Equals("E", StringComparison.OrdinalIgnoreCase))
                    .ToDictionary(
                        e => e.LanguageCode,
                        e => new LanguageInfo(e.Name, e.Direction));
                
                if (discoveredLanguages.Count > 0)
                {
                    await dataPersister.SavePublicationLanguages(publicationCode, discoveredLanguages);
                }
            }

            // Verify English (E) is available (it will be seeded separately after discovery)
            var englishEntry = languageEntries.FirstOrDefault(e => e.LanguageCode.Equals("E", StringComparison.OrdinalIgnoreCase));
            if (englishEntry == default)
            {
                Logger.Warning("English (E) not found in discovered languages for publication {PublicationCode}. Skipping.", publicationCode);
                continue;
            }

            // Add English to language mappings (for reference, but don't process it here)
            languageCodeToInfo.TryAdd("E", new LanguageInfo(englishEntry.Name, englishEntry.Direction));
            AddPublicationToLanguage("E", publicationCode, languageCodeToPublications);
        }
    }

    private async Task<List<(string LanguageCode, string Name, string Direction)>?> GetLanguageEntries(string publicationCode, string publicationName, bool isTestRun)
    {
        string jsonString;
        try
        {
            var harvestLink = $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?output=json&pub={publicationCode}&fileformat=MP3&alllangs=1&langwritten=E";
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
        List<(string LanguageCode, string Name, string Direction)> languageEntries,
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
        (string LanguageCode, string Name, string Direction) entry,
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


    internal async Task HarvestMusicMelodyLinks(bool isTestRun = false)
    {
        var discs = new List<string>();
        var downloadCodes = new List<string>();

        // Melody music publication codes (from centralized JwSourceHelper)
        foreach (var publicationCode in SharedHelpers.JwSourceHelper.MelodyMusicPublicationCodes)
        {
            Logger.Information("Starting harvest for Instrumental Music publication: {PublicationCode}", publicationCode);
            
            downloadCodes.Clear();
            if (publicationCode == "iam")
            {
                for (var i = 1; i <= 9; i++)
                {
                    if (i is 7 or 8)
                    {
                        continue;
                    }

                    downloadCodes.Add($"{publicationCode}-{i}");
                }
            }
            else
            {
                downloadCodes.Add(publicationCode);
            }

            Logger.Information("Harvesting Music track links for {PublicationCode}.", publicationCode);
            await HarvestMusicLinks(publicationCode, downloadCodes);
        }
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
                    MusicTrackHarvestParsing.SaveMusicTracks(discDir, discFile, disc.Value);
                    
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
                MusicTrackHarvestParsing.SaveMusicTracks(dir, $"{dir}/tracks.json", allTracks);
            }
            
            return true;
        }
        else
        {
            // Original logic for other publications
            var file = $"{dir}/tracks.json";
            var trackCode = 1;
            var musicTracks = new List<MusicTrack>();
            string? localizedPubName = null;

            foreach (var publicationDownloadCode in publicationDownloadCodes)
            {
                var result = await FetchAndProcessMusicFiles(publicationDownloadCode, publicationCode, languageCode, trackCode, musicTracks);
                trackCode = result.TrackCode;

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
                // Use localized publication name from API response, fallback to publication code
                var finalPublicationName = localizedPubName ?? publicationCode;
                await dataPersister.SaveMusicTracks(publicationCode, languageCode, finalPublicationName, musicTracks);
            }
            else
            {
                MusicTrackHarvestParsing.SaveMusicTracks(dir, file, musicTracks);
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

    private async Task<(int TrackCode, string? LocalizedPubName, string? DiscName)> FetchAndProcessMusicFiles(
        string publicationDownloadCode,
        string publicationCode,
        string? languageCode,
        int trackCode,
        List<MusicTrack> musicTracks)
    {
        string jsonString;
        try
        {
            var harvestLink = MusicTrackHarvestParsing.BuildMusicHarvestLink(publicationDownloadCode, languageCode);
            jsonString = await DownloadUtility.GetAsync(harvestLink);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("Response status code"))
        {
            return (trackCode, null, null);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to fetch tracks for publication {PublicationCode}. Skipping.", publicationDownloadCode);
            return (trackCode, null, null);
        }

        // Parse and process within the same scope to keep JsonDocument alive
        using var doc = JsonDocument.Parse(jsonString);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("files", out var filesElement))
        {
            return (trackCode, null, null);
        }

        var lc = languageCode ?? "E";
        if (!filesElement.TryGetProperty(lc, out var languageFiles) ||
            !languageFiles.TryGetProperty("MP3", out var musicFiles))
        {
            return (trackCode, null, null);
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

        var newTrackCode = MusicTrackHarvestParsing.ProcessMusicFiles(musicFiles, publicationDownloadCode, languageCode, trackCode, musicTracks);
        return (newTrackCode, localizedPubName, discName);
    }

}
