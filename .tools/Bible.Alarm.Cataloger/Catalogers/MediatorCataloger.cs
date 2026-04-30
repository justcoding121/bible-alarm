#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Cataloger.Models;
using Bible.Alarm.Cataloger.Utility;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Serilog;
using DirectoryHelper = Bible.Alarm.Cataloger.Utility.DirectoryHelper;
using DownloadUtilityType = Bible.Alarm.Cataloger.Utility.DownloadUtility;

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

    internal async Task CatalogMediatorLinks(IReadOnlySet<string>? publicationFilter = null)
    {
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
                publicationCode);
        }

    }

    private async Task CatalogMediatorPublication(
        string publicationCode,
        string publicationName)
    {
        var categoryKey = JwSourceHelper.GetMediatorCategoryKey(publicationCode);
        var pathAndQuery = $"{AppConstants.ApiEndpoints.MediatorApiCategoriesPathPrefix}/{AppConstants.Media.DefaultLanguageCode}/{categoryKey}";
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
                .Where(kvp => !kvp.Key.Equals(AppConstants.Media.DefaultLanguageCode, StringComparison.OrdinalIgnoreCase))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            
            if (languagesToSave.Count > 0)
            {
                await dataPersister.SavePublicationLanguages(publicationCode, languagesToSave);
            }
        }

        // Verify English (E) is available (it will be seeded separately after discovery)
        if (!languagesFromCategory.Contains(AppConstants.Media.DefaultLanguageCode, StringComparer.OrdinalIgnoreCase))
        {
            Logger.Warning("English (E) not found in discovered languages for publication {PublicationCode}. Skipping.", publicationCode);
            return;
        }

        Logger.Information("English (E) found for publication {PublicationName} - will be seeded separately", publicationName);
    }
}

