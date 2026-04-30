#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Cataloger.Models;
using Bible.Alarm.Cataloger.Utility;
using Bible.Alarm.Shared.Constants;
using Serilog;
using SharedHelpers = Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Cataloger.Catalogers;

internal class MusicCataloger : BaseCataloger
{
    private readonly IDataPersister? dataPersister;
    private readonly SignLanguageChecker signLanguageChecker;

    public MusicCataloger(ILogger logger, DownloadUtility downloadUtility, IDataPersister? dataPersister = null)
        : base(logger, downloadUtility)
    {
        this.dataPersister = dataPersister;
        signLanguageChecker = new SignLanguageChecker(logger, downloadUtility);
    }


    /// <summary>
    /// Localized publication names: (languageCode, publicationCode) -> localizedName
    /// </summary>
    private readonly Dictionary<(string LanguageCode, string PublicationCode), string> localizedVocalNames =
        new(SharedHelpers.PublicationLookupKeyComparers.LanguagePublication.Instance);

    internal async Task CatalogVocalMusicLinks(bool isTestRun = false, IReadOnlySet<string>? publicationFilter = null)
    {
        var languageCodeToInfo = new Dictionary<string, LanguageInfo>(StringComparer.OrdinalIgnoreCase);
        var languageCodeToPublications = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        var vocalMusicPublicationCodes = SharedHelpers.JwSourceHelper.VocalMusicPublicationCodes;
        var codesToCatalog = publicationFilter != null ? vocalMusicPublicationCodes.Where(c => publicationFilter.Contains(c)).ToList() : vocalMusicPublicationCodes.ToList();
        foreach (var publicationCode in codesToCatalog)
        {
            Logger.Information("Starting catalog for Vocal Music publication: {PublicationCode}", publicationCode);
            
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
            await SaveDiscoveredNonEnglishPublicationLanguagesAsync(publicationCode, languageEntries);

            // Verify English (E) is available (it will be seeded separately after discovery)
            var englishEntry = languageEntries.FirstOrDefault(e => e.LanguageCode.Equals(AppConstants.Media.DefaultLanguageCode, StringComparison.OrdinalIgnoreCase));
            if (englishEntry == default)
            {
                Logger.Warning("English (E) not found in discovered languages for publication {PublicationCode}. Skipping.", publicationCode);
                continue;
            }

            // Add English to language mappings (for reference, but don't process it here)
            languageCodeToInfo.TryAdd(AppConstants.Media.DefaultLanguageCode, new LanguageInfo(englishEntry.Name, englishEntry.Direction));
            AddPublicationToLanguage(AppConstants.Media.DefaultLanguageCode, publicationCode, languageCodeToPublications);
        }
    }

    internal async Task CatalogArticleSeriesLinks(bool isTestRun = false, IReadOnlySet<string>? publicationFilter = null) =>
        await CatalogFlatAudioPublicationLinks(SharedHelpers.JwSourceHelper.ArticleSeriesPublicationCodes, AppConstants.Media.CatalogFlatAudioLabelArticleSeries, isTestRun, publicationFilter);

    internal async Task CatalogBooksLinks(bool isTestRun = false, IReadOnlySet<string>? publicationFilter = null) =>
        await CatalogFlatAudioPublicationLinks(SharedHelpers.JwSourceHelper.BooksPublicationCodes, AppConstants.Media.CatalogFlatAudioLabelBooks, isTestRun, publicationFilter);

    internal async Task CatalogYearbooksLinks(bool isTestRun = false, IReadOnlySet<string>? publicationFilter = null) =>
        await CatalogFlatAudioPublicationLinks(SharedHelpers.JwSourceHelper.YearbooksPublicationCodes, AppConstants.Media.CatalogFlatAudioLabelYearbooks, isTestRun, publicationFilter);

    internal async Task CatalogBrochuresAndBookletsLinks(bool isTestRun = false, IReadOnlySet<string>? publicationFilter = null) =>
        await CatalogFlatAudioPublicationLinks(SharedHelpers.JwSourceHelper.BrochuresAndBookletsPublicationCodes, AppConstants.Media.CatalogFlatAudioLabelBrochuresAndBooklets, isTestRun, publicationFilter);

    private async Task CatalogFlatAudioPublicationLinks(
        IReadOnlyCollection<string> publicationCodes,
        string categoryLabel,
        bool isTestRun,
        IReadOnlySet<string>? publicationFilter = null)
    {
        var codesToCatalog = publicationFilter != null
            ? publicationCodes.Where(c => publicationFilter.Contains(c)).ToList()
            : publicationCodes.ToList();
        if (codesToCatalog.Count == 0)
        {
            return;
        }

        foreach (var publicationCode in codesToCatalog)
        {
            Logger.Information("Starting catalog for {CategoryLabel} publication: {PublicationCode}", categoryLabel, publicationCode);

            var languageEntries = await GetLanguageEntries(publicationCode, publicationCode, isTestRun);
            if (languageEntries == null || languageEntries.Count == 0)
            {
                Logger.Warning("No languages found for {CategoryLabel} publication: {PublicationCode}", categoryLabel, publicationCode);
                continue;
            }

            Logger.Information("Found {Count} language(s) for {CategoryLabel} publication: {PublicationCode}",
                languageEntries.Count, categoryLabel, publicationCode);

            languageEntries = await signLanguageChecker.FilterSignLanguagesAsync(languageEntries);

            if (languageEntries.Count == 0)
            {
                Logger.Warning("No non-sign languages found for {CategoryLabel} publication: {PublicationCode}", categoryLabel, publicationCode);
                continue;
            }

            await SaveDiscoveredNonEnglishPublicationLanguagesAsync(publicationCode, languageEntries);

            var englishEntry = languageEntries.FirstOrDefault(e => e.LanguageCode.Equals(AppConstants.Media.DefaultLanguageCode, StringComparison.OrdinalIgnoreCase));
            if (englishEntry == default)
            {
                Logger.Warning("English (E) not found in discovered languages for publication {PublicationCode}. Skipping.", publicationCode);
            }
        }
    }

    private async Task SaveDiscoveredNonEnglishPublicationLanguagesAsync(
        string publicationCode,
        List<(string LanguageCode, string Name, string Direction)> languageEntries)
    {
        if (dataPersister == null)
        {
            return;
        }

        var discoveredLanguages = languageEntries
            .Where(e => !e.LanguageCode.Equals(AppConstants.Media.DefaultLanguageCode, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(
                e => e.LanguageCode,
                e => new LanguageInfo(e.Name, e.Direction),
                StringComparer.OrdinalIgnoreCase);

        if (discoveredLanguages.Count > 0)
        {
            await dataPersister.SavePublicationLanguages(publicationCode, discoveredLanguages);
        }
    }

    private async Task<List<(string LanguageCode, string Name, string Direction)>?> GetLanguageEntries(string publicationCode, string publicationName, bool isTestRun)
    {
        string jsonString;
        try
        {
            var catalogLink = $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?{AppConstants.Media.GetPubQueryOutputJson}&{AppConstants.Media.GetPubQueryParamName.Pub}={publicationCode}&{AppConstants.Media.GetPubQueryParamName.FileFormat}={AppConstants.Media.MediaStreamFormatMp3}&{AppConstants.Media.GetPubQueryAllLangsOn}&{AppConstants.Media.GetPubQueryParamLangWritten}={AppConstants.Media.DefaultLanguageCode}";
            jsonString = await DownloadUtility.GetAsync(catalogLink);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("Response status code", StringComparison.Ordinal))
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


    internal async Task CatalogMusicMelodyLinks(bool isTestRun = false, IReadOnlySet<string>? publicationFilter = null)
    {
        var downloadCodes = new List<string>();

        var melodyCodes = SharedHelpers.JwSourceHelper.MelodyMusicPublicationCodes;
        var codesToCatalog = publicationFilter != null ? melodyCodes.Where(c => publicationFilter.Contains(c)).ToList() : melodyCodes.ToList();
        foreach (var publicationCode in codesToCatalog)
        {
            Logger.Information("Starting catalog for Instrumental Music publication: {PublicationCode}", publicationCode);
            
            downloadCodes.Clear();
            if (publicationCode == AppConstants.Media.MelodyMusicPublicationCodeIam)
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

            Logger.Information("Cataloging Music track links for {PublicationCode}.", publicationCode);
            await CatalogMusicLinks(publicationCode, downloadCodes);
        }
    }

    private async Task<bool> CatalogMusicLinks(string publicationCode, List<string> publicationDownloadCodes, string? languageCode = null)
    {
        var dir = GetMusicDirectory(publicationCode, languageCode);

        // For iam (Kingdom Melodies), save tracks grouped by disc
        if (publicationCode == AppConstants.Media.MelodyMusicPublicationCodeIam && languageCode == null)
        {
            var discTracksMap = new Dictionary<string, List<MusicTrack>>(StringComparer.OrdinalIgnoreCase);
            var discNamesMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

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
                    var discFile = $"{discDir}/{AppConstants.ApiEndpoints.MediaIndexTracksFileName}";
                    MusicTrackCatalogParsing.SaveMusicTracks(discDir, discFile, disc.Value);
                    
                    // Save disc info (name) if available
                    if (discNamesMap.TryGetValue(disc.Key, out var discName))
                    {
                        var discInfoFile = $"{discDir}/{AppConstants.ApiEndpoints.MediaIndexMelodyDiscInfoFileName}";
                        var discInfo = new { Code = disc.Key, Name = discName };
                        await File.WriteAllTextAsync(discInfoFile, JsonSerializer.Serialize(discInfo));
                    }
                }

                // Also save a main tracks.json with all tracks for backward compatibility
                var allTracks = discTracksMap.Values.SelectMany(t => t).OrderBy(t => t.Number).ToList();
                MusicTrackCatalogParsing.SaveMusicTracks(dir, $"{dir}/{AppConstants.ApiEndpoints.MediaIndexTracksFileName}", allTracks);
            }
            
            return true;
        }
        else
        {
            // Original logic for other publications
            var file = $"{dir}/{AppConstants.ApiEndpoints.MediaIndexTracksFileName}";
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
                MusicTrackCatalogParsing.SaveMusicTracks(dir, file, musicTracks);
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
            return $"{DirectoryHelper.IndexDirectory}/{AppConstants.FilePaths.MediaIndexCatalogRootMediaSegment}/{AppConstants.Media.BiblePublicationCategoryMusic}/{AppConstants.ApiEndpoints.MediaIndexFolderMelodies}/{normalizedPublicationCode}";
        }
        else
        {
            // Vocals: with language
            var normalizedLanguageCode = languageCode.ToUpperInvariant();
            var normalizedPublicationCode = publicationCode.ToUpperInvariant();
            return $"{DirectoryHelper.IndexDirectory}/{AppConstants.FilePaths.MediaIndexCatalogRootMediaSegment}/{AppConstants.Media.BiblePublicationCategoryMusic}/{AppConstants.ApiEndpoints.MediaIndexFolderVocals}/{normalizedLanguageCode}/{normalizedPublicationCode}";
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
            var catalogLink = MusicTrackCatalogParsing.BuildMusicCatalogLink(publicationDownloadCode, languageCode);
            jsonString = await DownloadUtility.GetAsync(catalogLink);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("Response status code", StringComparison.Ordinal))
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

        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(AppConstants.Media.PubMediaJson.Files, out var filesElement))
        {
            return (trackCode, null, null);
        }

        var lc = languageCode ?? "E";
        if (!filesElement.TryGetProperty(lc, out var languageFiles) ||
            !languageFiles.TryGetProperty(AppConstants.Media.MediaStreamFormatMp3, out var musicFiles))
        {
            return (trackCode, null, null);
        }

        // Extract localized publication name from pubName field
        string? localizedPubName = null;
        string? discName = null;
        if (root.TryGetProperty(AppConstants.Media.PubMediaJson.PubName, out var pubNameElement))
        {
            var rawName = pubNameElement.GetString();
            // Decode HTML entities like &nbsp; to proper characters and replace non-breaking spaces with regular spaces
            var decodedName = SharedHelpers.MediaTrackTitleHelper.DecodeHtmlTitleNullable(rawName);
            
            // For iam (Kingdom Melodies), pubName is the disc name (e.g., "Kingdom Melodies, Volume 1")
            if (publicationCode == AppConstants.Media.MelodyMusicPublicationCodeIam && languageCode == null)
            {
                discName = decodedName;
            }
            else
            {
                localizedPubName = decodedName;
            }
        }

        var newTrackCode = MusicTrackCatalogParsing.ProcessMusicFiles(musicFiles, publicationDownloadCode, languageCode, trackCode, musicTracks);
        return (newTrackCode, localizedPubName, discName);
    }

}
