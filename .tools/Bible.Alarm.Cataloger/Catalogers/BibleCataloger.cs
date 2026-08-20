#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bible.Alarm.Cataloger.Models;
using Bible.Alarm.Cataloger.Utility;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Serilog;

namespace Bible.Alarm.Cataloger.Catalogers;

internal class BibleCataloger : BaseCataloger
{
    private readonly IDataPersister? dataPersister;
    private readonly BibleLanguageDiscoveryCataloger discoveryCataloger;

    public BibleCataloger(ILogger logger, DownloadUtility downloadUtility, IDataPersister? dataPersister = null)
        : base(logger, downloadUtility)
    {
        this.dataPersister = dataPersister;
        discoveryCataloger = new BibleLanguageDiscoveryCataloger(logger, downloadUtility, dataPersister);
    }

    /// <summary>
    /// Dictionary to store localized publication names: (languageCode, publicationCode) -> localizedName
    /// </summary>
    private readonly ConcurrentDictionary<(string LanguageCode, string PublicationCode), string> localizedPublicationNames =
        new(PublicationLookupKeyComparers.LanguagePublication.Instance);

    /// <summary>
    /// Key: (languageCode, publicationCode), Value: localized publication name
    /// </summary>
    public IReadOnlyDictionary<(string LanguageCode, string PublicationCode), string> LocalizedPublicationNames => localizedPublicationNames;

    /// <summary>
    /// Discovery phase: Discovers all languages for all publications and sections using alllangs=1 and langwritten=E.
    /// This runs FIRST before any cataloging or seeding.
    /// </summary>
    internal async Task DiscoverLanguages(
        Dictionary<string, string> biblePublicationCodeToNameMappings,
        bool isTestRun = false)
    {
        await discoveryCataloger.DiscoverLanguages(biblePublicationCodeToNameMappings, isTestRun);
    }

    internal async Task CatalogBibleLinks(
        Dictionary<string, string> biblePublicationCodeToNameMappings,
        ConcurrentDictionary<string, LanguageInfo> languageCodeToInfoMappings,
        ConcurrentDictionary<string, List<string>> languageCodeToEditionsMapping,
        bool isTestRun = false)
    {
        foreach (var publication in biblePublicationCodeToNameMappings)
        {
            await CatalogSingleBiblePublicationLinksAsync(
                publication,
                languageCodeToInfoMappings,
                languageCodeToEditionsMapping,
                isTestRun);
        }
    }

    private async Task CatalogSingleBiblePublicationLinksAsync(
        KeyValuePair<string, string> publication,
        ConcurrentDictionary<string, LanguageInfo> languageCodeToInfoMappings,
        ConcurrentDictionary<string, List<string>> languageCodeToEditionsMapping,
        bool isTestRun)
    {
        var publicationCode = publication.Key;
        Logger.Information("Starting catalog for publication: {PublicationCode} ({PublicationName})", publicationCode, publication.Value);

        Dictionary<string, LanguageInfo>? allDiscoveredLanguages = null;

        if (dataPersister is DbSeeder dbSeeder)
        {
            var normalizedPublicationCode = publicationCode.ToLowerInvariant();
            if (dbSeeder.PublicationLanguages.TryGetValue(normalizedPublicationCode, out var discoveredLangs))
            {
                allDiscoveredLanguages = discoveredLangs;
                Logger.Debug("Using discovered languages from discovery phase for publication {PublicationCode}", publicationCode);
            }
        }

        if (allDiscoveredLanguages == null || allDiscoveredLanguages.Count == 0)
        {
            Logger.Warning("No discovered languages found for publication {PublicationCode} in data store. Running discovery now...", publicationCode);
            allDiscoveredLanguages = await discoveryCataloger.DiscoverLanguagesForAllBooks(publicationCode, publication.Value, isTestRun);

            if (allDiscoveredLanguages == null || allDiscoveredLanguages.Count == 0)
            {
                Logger.Warning("No languages discovered for publication {PublicationCode}. Skipping.", publicationCode);
                return;
            }

            if (dataPersister != null)
            {
                await dataPersister.SavePublicationLanguages(publicationCode, allDiscoveredLanguages);
            }
        }

        if (!allDiscoveredLanguages.TryGetValue(AppConstants.Media.DefaultLanguageCode, out var englishLanguageInfo))
        {
            Logger.Warning("English (E) not found in discovered languages for publication {PublicationCode}. Skipping.", publicationCode);
            return;
        }

        languageCodeToInfoMappings.TryAdd(AppConstants.Media.DefaultLanguageCode, englishLanguageInfo);
        if (!languageCodeToEditionsMapping.TryAdd(AppConstants.Media.DefaultLanguageCode, [publicationCode]))
        {
            languageCodeToEditionsMapping[AppConstants.Media.DefaultLanguageCode].Add(publicationCode);
        }
    }
}
