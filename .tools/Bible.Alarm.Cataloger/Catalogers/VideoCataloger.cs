#nullable enable

using System;
using System.Collections.Generic;
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
        new KeyValuePair<string, string>(AppConstants.Media.BiblePublicationCodeDramasGoodNews, AppConstants.Media.PublicationDisplayNameGoodNewsAccordingToJesus)
    ]);

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
                e => new LanguageInfo(e.Name, e.Direction),
                StringComparer.OrdinalIgnoreCase);

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
            var catalogLink = $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?{AppConstants.Media.GetPubQueryOutputJson}&{AppConstants.Media.GetPubQueryParamName.Pub}={publicationCode}&{AppConstants.Media.GetPubQueryParamName.FileFormat}={AppConstants.Media.MediaStreamFormatMp4}&{AppConstants.Media.GetPubQueryAllLangsOn}&{AppConstants.Media.GetPubQueryParamLangWritten}={AppConstants.Media.DefaultLanguageCode}";
            jsonString = await DownloadUtility.GetAsync(catalogLink);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("Response status code", StringComparison.Ordinal))
        {
            Logger.Warning(ex, "No languages found for Video publication: {PublicationName} ({PublicationCode})",
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

    private static void AddPublicationToLanguage(
        string languageCode,
        string publicationCode,
        Dictionary<string, List<string>> languageCodeToPublications)
    {
        lock (languageCodeToPublications)
        {
            if (!languageCodeToPublications.TryGetValue(languageCode, out var publications))
            {
                publications = [];
                languageCodeToPublications[languageCode] = publications;
            }

            if (!publications.Contains(publicationCode))
            {
                publications.Add(publicationCode);
            }
        }
    }

}
