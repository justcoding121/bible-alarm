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
using DownloadUtilityType = Bible.Alarm.Cataloger.Utility.DownloadUtility;
using SharedHelpers = Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Cataloger.Catalogers;

internal class VideoCataloger : BaseCataloger
{
    private readonly IDataPersister? dataPersister;
    private readonly SignLanguageChecker signLanguageChecker;

    public VideoCataloger(ILogger logger, DownloadUtility downloadUtility, IDataPersister? dataPersister = null)
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
        new KeyValuePair<string, string>("DramasGoodNews", "The Good News According to Jesus")
    ]);

    /// <summary>
    /// Maps publication codes to Mediator API category keys for fetching localized names (pub code = category key for video).
    /// </summary>
    private static readonly Dictionary<string, string> PublicationCodeToCategoryKey = new([
        new KeyValuePair<string, string>("DramasGoodNews", "DramasGoodNews")
    ]);

    /// <summary>
    /// Localized publication names: (languageCode, publicationCode) -> localizedName
    /// </summary>
    private readonly Dictionary<(string LanguageCode, string PublicationCode), string> localizedPublicationNames = new();
    private readonly object localizedNamesLock = new();


    internal async Task CatalogVideoLinks(bool isTestRun = false, IReadOnlySet<string>? publicationFilter = null)
    {
        var languageCodeToInfo = new Dictionary<string, LanguageInfo>(StringComparer.OrdinalIgnoreCase);
        var languageCodeToPublications = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        // Only catalog here publications that use GETPUBMEDIALINKS for language discovery.
        // All video/drama (DramasGoodNews, VOD*, etc.) use the Mediator API and are cataloged by MediatorCataloger.
        var videoCodes = publicationFilter != null
            ? SharedHelpers.JwSourceHelper.VideoPublicationCodes.Where(c => publicationFilter.Contains(c)).ToList()
            : SharedHelpers.JwSourceHelper.VideoPublicationCodes.ToList();
        videoCodes = videoCodes
            .Where(c => SharedHelpers.JwSourceHelper.MediatorValidationExclusionCodes.Contains(c))
            .ToList();
        foreach (var publicationCode in videoCodes)
        {
            var publicationName = VideoPublicationCodeToNameMappings.GetValueOrDefault(publicationCode, publicationCode);
            Logger.Information("Starting catalog for Video publication: {PublicationName} ({PublicationCode})", 
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
            await SaveDiscoveredNonEnglishPublicationLanguagesAsync(publicationCode, languageEntries);

            // Verify English (E) is available (it will be seeded separately after discovery)
            var englishEntry = languageEntries.FirstOrDefault(e => e.Code.Equals(AppConstants.Media.DefaultLanguageCode, StringComparison.OrdinalIgnoreCase));
            if (englishEntry == default)
            {
                Logger.Warning("English (E) not found in discovered languages for publication {PublicationCode}. Skipping.", publicationCode);
                continue;
            }

            // Add English to language mappings (for reference, but don't process it here)
            languageCodeToInfo[englishEntry.Code] = new LanguageInfo(englishEntry.Name, englishEntry.Direction);
            AddPublicationToLanguage(englishEntry.Code, publicationCode, languageCodeToPublications);
        }

        // Only use GETPUBMEDIALINKS for series that are not Mediator-only (e.g. thv). Mediator series are cataloged by MediatorCataloger.
        var seriesCodes = publicationFilter != null
            ? SharedHelpers.JwSourceHelper.SeriesPublicationCodes.Where(c => publicationFilter.Contains(c)).ToList()
            : SharedHelpers.JwSourceHelper.SeriesPublicationCodes.ToList();
        seriesCodes = seriesCodes
            .Where(c => !SharedHelpers.JwSourceHelper.AllMediatorPublicationCodes.Contains(c))
            .ToList();
        foreach (var publicationCode in seriesCodes)
        {
            var publicationName = VideoPublicationCodeToNameMappings.GetValueOrDefault(publicationCode, publicationCode);
            Logger.Information("Starting catalog for Series publication: {PublicationName} ({PublicationCode})",
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

            await SaveDiscoveredNonEnglishPublicationLanguagesAsync(publicationCode, languageEntries);

            var englishEntry = languageEntries.FirstOrDefault(e => e.Code.Equals(AppConstants.Media.DefaultLanguageCode, StringComparison.OrdinalIgnoreCase));
            if (englishEntry == default)
            {
                Logger.Warning("English (E) not found in discovered languages for publication {PublicationCode}. Skipping.", publicationCode);
                continue;
            }

            languageCodeToInfo[englishEntry.Code] = new LanguageInfo(englishEntry.Name, englishEntry.Direction);
            AddPublicationToLanguage(englishEntry.Code, publicationCode, languageCodeToPublications);
        }
    }

    private async Task SaveDiscoveredNonEnglishPublicationLanguagesAsync(
        string publicationCode,
        List<(string Code, string Name, string Direction)> languageEntries)
    {
        if (dataPersister == null)
        {
            return;
        }

        var discoveredLanguages = languageEntries
            .Where(e => !e.Code.Equals(AppConstants.Media.DefaultLanguageCode, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(
                e => e.Code,
                e => new LanguageInfo(e.Name, e.Direction));

        if (discoveredLanguages.Count > 0)
        {
            await dataPersister.SavePublicationLanguages(publicationCode, discoveredLanguages);
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
            var catalogLink = $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?{AppConstants.Media.GetPubQueryOutputJson}&pub={publicationCode}&fileformat={AppConstants.Media.MediaStreamFormatMp4}&{AppConstants.Media.GetPubQueryAllLangsOn}&langwritten={AppConstants.Media.DefaultLanguageCode}";
            jsonString = await DownloadUtility.GetAsync(catalogLink);
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
        Logger.Information("Cataloging Video episode links for {PublicationName} in {Language} language.",
            publicationName, language);

        try
        {
            var success = await CatalogVideoEpisodes(publicationCode, languageCode, publicationName);
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
            Logger.Error(e, "Failed: Cataloging Video episode links for {PublicationName} in {Language} language.",
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
            var pathAndQuery = $"/categories/{languageCode}/{categoryKey}";
            var jsonString = await DownloadUtilityType.GetMediatorAsync(pathAndQuery);
            if (string.IsNullOrEmpty(jsonString))
            {
                return;
            }

            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            if (root.TryGetProperty(AppConstants.Media.PubMediaJson.Category, out var category) &&
                category.TryGetProperty(AppConstants.Media.PubMediaJson.Name, out var nameElement))
            {
                var rawName = nameElement.GetString();
                // Decode HTML entities like &nbsp; to proper characters and replace non-breaking spaces with regular spaces
                var localizedName = SharedHelpers.MediaTrackTitleHelper.DecodeHtmlTitleNullable(rawName);
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

    private async Task<bool> CatalogVideoEpisodes(string publicationCode, string languageCode, string publicationName)
    {
        var normalizedLanguageCode = languageCode.ToUpperInvariant();
        var normalizedPublicationCode = publicationCode.ToUpperInvariant();
        var dir = $"{DirectoryHelper.IndexDirectory}/media/Dramas/{normalizedLanguageCode}/{normalizedPublicationCode}";
        var file = $"{dir}/episodes.json";

        var episodes = await FetchAllEpisodes(publicationCode, languageCode);
        if (episodes == null || episodes.Count == 0)
        {
            return false;
        }

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

    private async Task<List<VideoEpisode>?> FetchAllEpisodes(string publicationCode, string languageCode)
    {
        try
        {
            var catalogLink = $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?{AppConstants.Media.GetPubQueryOutputJson}&pub={publicationCode}&fileformat={AppConstants.Media.MediaStreamFormatMp4}&langwritten={languageCode}";
            var jsonString = await DownloadUtility.GetAsync(catalogLink);
            return ParseAllEpisodes(jsonString, publicationCode, languageCode);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("Response status code"))
        {
            return null;
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Failed to fetch episodes for {PublicationCode} in {LanguageCode}", publicationCode, languageCode);
            return null;
        }
    }

    private static List<VideoEpisode>? ParseAllEpisodes(string? jsonString, string publicationCode, string languageCode)
    {
        if (string.IsNullOrEmpty(jsonString))
            return null;

        using var doc = JsonDocument.Parse(jsonString);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(AppConstants.Media.PubMediaJson.Files, out var filesElement))
            return null;

        if (!filesElement.TryGetProperty(languageCode, out var languageFiles))
        {
            if (!filesElement.TryGetProperty(languageCode.ToUpperInvariant(), out languageFiles))
                return null;
        }

        if (!languageFiles.TryGetProperty(AppConstants.Media.MediaStreamFormatMp4, out var mp4Files) || mp4Files.ValueKind != JsonValueKind.Array)
            return null;

        var lookUpPathBase = $"?{AppConstants.Media.GetPubQueryOutputJson}&pub={publicationCode}&fileformat={AppConstants.Media.MediaStreamFormatMp4}&langwritten={languageCode}";
        var episodes = new List<VideoEpisode>();

        foreach (var fileElement in mp4Files.EnumerateArray())
        {
            if (!fileElement.TryGetProperty(AppConstants.Media.PubMediaJson.Track, out var trackEl) || trackEl.ValueKind != JsonValueKind.Number)
                continue;
            var episodeNumber = trackEl.GetInt32();
            if (episodeNumber == 0)
                continue;

            if (!fileElement.TryGetProperty(AppConstants.Media.PubMediaJson.File, out var fileInfo) ||
                !fileInfo.TryGetProperty(AppConstants.Media.PubMediaJson.Url, out var urlElement))
                continue;

            var url = urlElement.GetString();
            if (string.IsNullOrEmpty(url))
                continue;

            var title = SharedHelpers.MediaTrackTitleHelper.UnknownTitle;
            if (fileElement.TryGetProperty(AppConstants.Media.PubMediaJson.Title, out var titleElement))
            {
                title = SharedHelpers.MediaTrackTitleHelper.DecodeHtmlTitle(titleElement.GetString());
            }

            double duration = 0;
            if (fileElement.TryGetProperty(AppConstants.Media.PubMediaJson.Duration, out var durationElement))
                duration = durationElement.GetDouble();

            episodes.Add(new VideoEpisode
            {
                Number = episodeNumber,
                Title = title,
                Url = url,
                LookUpPath = lookUpPathBase,
                Duration = duration
            });
        }

        return episodes.Count > 0 ? episodes.OrderBy(e => e.Number).ToList() : null;
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
