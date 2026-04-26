#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Cataloger.Models;
using Bible.Alarm.Cataloger.Utility;
using DownloadUtilityType = Bible.Alarm.Cataloger.Utility.DownloadUtility;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Serilog;
using DirectoryHelper = Bible.Alarm.Cataloger.Utility.DirectoryHelper;

namespace Bible.Alarm.Cataloger.Catalogers;

internal class MediatorCataloger : BaseCataloger
{
    private readonly IDataPersister? dataPersister;
    private readonly SignLanguageChecker signLanguageChecker;

    public MediatorCataloger(ILogger logger, DownloadUtility downloadUtility, IDataPersister? dataPersister = null)
        : base(logger, downloadUtility)
    {
        this.dataPersister = dataPersister;
        signLanguageChecker = new SignLanguageChecker(logger, downloadUtility);
    }

    /// <summary>
    /// Localized category names: (languageCode, categoryKey) -> localizedName (from API response per language).
    /// </summary>
    private readonly ConcurrentDictionary<(string LanguageCode, string CategoryKey), string> localizedCategoryNames = new();

    internal async Task CatalogMediatorLinks(bool isTestRun = false, IReadOnlySet<string>? publicationFilter = null)
    {
        // Track publications per language: languageCode -> set of publication codes
        var languageCodeToPublications = new ConcurrentDictionary<string, ConcurrentDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        // Catalog each Mediator API publication (dramas, series, children, broadcasting, family, etc.).
        var mediatorCodes = JwSourceHelper.AllMediatorPublicationCodes;
        var codesToCatalog = publicationFilter != null
            ? mediatorCodes.Where(c => publicationFilter.Contains(c)).ToList()
            : mediatorCodes.ToList();
        foreach (var publicationCode in codesToCatalog)
        {
            Logger.Information("Cataloging Mediator publication: {PublicationCode}", publicationCode);

            await CatalogMediatorPublication(
                publicationCode,
                publicationCode,
                languageCodeToPublications,
                isTestRun);
        }

    }

    private async Task CatalogMediatorPublication(
        string publicationCode,
        string publicationName,
        ConcurrentDictionary<string, ConcurrentDictionary<string, string>> languageCodeToPublications,
        bool isTestRun)
    {
        var categoryKey = JwSourceHelper.GetMediatorCategoryKey(publicationCode);
        var pathAndQuery = $"/categories/E/{categoryKey}";
        string? jsonString;
        try
        {
            jsonString = await DownloadUtilityType.GetMediatorAsync(pathAndQuery);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to fetch publication {PublicationCode} for English. Skipping.", publicationCode);
            return;
        }

        if (string.IsNullOrEmpty(jsonString))
        {
            Logger.Error("Empty response for publication {PublicationCode} for English. Skipping.", publicationCode);
            return;
        }

        // Parse the category response to get all unique languages
        var languagesFromCategory = MediatorCategoryLanguageExtractor.ExtractLanguagesFromCategory(jsonString, Logger);
        if (languagesFromCategory.Count == 0)
        {
            Logger.Warning("No languages found for publication {PublicationCode}. Skipping.", publicationCode);
            return;
        }

        // Filter out sign languages
        languagesFromCategory = await signLanguageChecker.FilterSignLanguagesAsync(languagesFromCategory);

        if (languagesFromCategory.Count == 0)
        {
            Logger.Warning("No non-sign languages found for publication {PublicationCode}. Skipping.", publicationCode);
            return;
        }

        // Extract language info from English category response
        var discoveredLanguages = MediatorCategoryLanguageExtractor.ExtractLanguageInfoFromCategory(jsonString, languagesFromCategory, Logger);

        // Filter out sign languages from discovered languages
        discoveredLanguages = await signLanguageChecker.FilterSignLanguagesAsync(discoveredLanguages);

        // Save discovered languages for on-demand fetching (excluding English)
        // The category API already lists only available languages, so no verification needed
        if (dataPersister != null && discoveredLanguages.Count > 0)
        {
            // Remove English from discovered languages since we're processing it
            var languagesToSave = discoveredLanguages
                .Where(kvp => !kvp.Key.Equals("E", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            
            if (languagesToSave.Count > 0)
            {
                await dataPersister.SavePublicationLanguages(publicationCode, languagesToSave);
            }
        }

        // Verify English (E) is available (it will be seeded separately after discovery)
        if (!languagesFromCategory.Contains("E", StringComparer.OrdinalIgnoreCase))
        {
            Logger.Warning("English (E) not found in discovered languages for publication {PublicationCode}. Skipping.", publicationCode);
            return;
        }

        Logger.Information("English (E) found for publication {PublicationName} - will be seeded separately", publicationName);
    }

    private async Task ProcessPublicationForLanguage(
        string publicationCode,
        string publicationName,
        string languageCode,
        ConcurrentDictionary<string, ConcurrentDictionary<string, string>> languageCodeToPublications)
    {
        Logger.Information("Processing {PublicationName} for language: {LanguageCode}", publicationName, languageCode);
        
        // Normalize language code to uppercase for consistent storage and comparison
        var normalizedLanguageCode = languageCode.ToUpperInvariant();
        Logger.Information("Cataloging {PublicationName} for language {LanguageCode}", publicationName, normalizedLanguageCode);

        var categoryKey = JwSourceHelper.GetMediatorCategoryKey(publicationCode);
        var pathAndQuery = $"/categories/{normalizedLanguageCode}/{categoryKey}";
        string? jsonString;
        try
        {
            jsonString = await DownloadUtilityType.GetMediatorAsync(pathAndQuery);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("404") || ex.Message.Contains("Response status code"))
        {
            Logger.Warning("Publication {PublicationCode} not available for language {LanguageCode}. Skipping.", publicationCode, normalizedLanguageCode);
            return;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to fetch publication {PublicationCode} for language {LanguageCode}. Skipping.", publicationCode, normalizedLanguageCode);
            return;
        }

        if (string.IsNullOrEmpty(jsonString))
        {
            Logger.Warning("Empty response for publication {PublicationCode} in language {LanguageCode}. Skipping.", publicationCode, normalizedLanguageCode);
            return;
        }

        var (allTracks, localizedPublicationName) = MediatorSectionCodeExtractor.ExtractTracksFromMediatorCategory(
            jsonString,
            publicationCode,
            normalizedLanguageCode,
            Logger);
        if (allTracks.Count == 0)
        {
            Logger.Warning("No tracks found for publication {PublicationCode} in language {LanguageCode}. Skipping.", publicationCode, normalizedLanguageCode);
            return;
        }

        if (!string.IsNullOrEmpty(localizedPublicationName))
        {
            localizedCategoryNames[(normalizedLanguageCode, publicationCode)] = localizedPublicationName;
        }

        var publicationsForLanguage = languageCodeToPublications.GetOrAdd(normalizedLanguageCode, _ => new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        var finalPublicationName = localizedPublicationName ?? publicationName;
        publicationsForLanguage[publicationCode] = finalPublicationName;

        var tracksBySection = new Dictionary<string, List<MediatorTrack>>(StringComparer.OrdinalIgnoreCase)
        {
            [publicationCode] = allTracks
        };
        var sectionNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (dataPersister != null)
        {
            await dataPersister.SaveMediatorPublication(normalizedLanguageCode, publicationCode, finalPublicationName, tracksBySection, sectionNames);
        }
        else
        {
            MediatorFilePersistence.SaveMediatorSectionsAndTracks(publicationCode, normalizedLanguageCode, tracksBySection, sectionNames);
        }

        Logger.Information("Saved {Count} tracks for publication {PublicationCode} ({LanguageCode})", allTracks.Count, publicationCode, normalizedLanguageCode);
    }
}
