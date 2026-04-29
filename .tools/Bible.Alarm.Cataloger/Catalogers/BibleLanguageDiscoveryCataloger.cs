#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Bible.Alarm.Cataloger.Models;
using Bible.Alarm.Cataloger.Utility;
using Bible.Alarm.Shared.Constants;
using Serilog;

namespace Bible.Alarm.Cataloger.Catalogers;

internal sealed class BibleLanguageDiscoveryCataloger : BaseCataloger
{
    private static readonly JsonSerializerOptions IndentedJsonSerializerOptions = new() { WriteIndented = true };

    private readonly IDataPersister? dataPersister;
    private readonly SignLanguageChecker signLanguageChecker;

    public BibleLanguageDiscoveryCataloger(ILogger logger, DownloadUtility downloadUtility, IDataPersister? dataPersister)
        : base(logger, downloadUtility)
    {
        this.dataPersister = dataPersister;
        signLanguageChecker = new SignLanguageChecker(logger, downloadUtility);
    }

    /// <summary>
    /// Discovery phase: Discovers all languages for all publications and sections using alllangs=1 and langwritten=E.
    /// This runs FIRST before any cataloging or seeding.
    /// </summary>
    internal async Task DiscoverLanguages(Dictionary<string, string> biblePublicationCodeToNameMappings, bool isTestRun = false)
    {
        Logger.Information("=== DISCOVERY PHASE: Discovering languages for all publications and sections ===");

        foreach (var publication in biblePublicationCodeToNameMappings)
        {
            var publicationCode = publication.Key;
            Logger.Information("Discovering languages for publication: {PublicationCode} ({PublicationName})", publicationCode, publication.Value);

            // Discover languages for each book (1-66) and save for English
            var allDiscoveredLanguages = await DiscoverLanguagesForAllBooks(publicationCode, publication.Value, isTestRun);

            if (allDiscoveredLanguages == null || allDiscoveredLanguages.Count == 0)
            {
                Logger.Warning("No languages discovered for publication {PublicationCode}. Skipping.", publicationCode);
                continue;
            }

            // Filter out sign languages
            allDiscoveredLanguages = await signLanguageChecker.FilterSignLanguagesAsync(allDiscoveredLanguages);

            if (allDiscoveredLanguages.Count == 0)
            {
                Logger.Warning("No non-sign languages discovered for publication {PublicationCode}. Skipping.", publicationCode);
                continue;
            }

            // Save language discovery results for English and publication languages for on-demand fetching
            if (dataPersister != null)
            {
                var languageCodeToNameMapping = allDiscoveredLanguages.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Name);
                await dataPersister.SaveLanguageDiscovery("E", publicationCode, languageCodeToNameMapping);
                await dataPersister.SavePublicationLanguages(publicationCode, allDiscoveredLanguages);
            }
            else
            {
                await SaveLanguageDiscoveryForEnglish(publicationCode, allDiscoveredLanguages);
            }
        }

        Logger.Information("=== DISCOVERY PHASE COMPLETED ===");
    }

    /// <summary>
    /// Discovers languages for each Bible book (1-66) using alllangs=1 and langwritten=E.
    /// Returns a consolidated dictionary of all unique languages discovered across all books.
    /// </summary>
    internal async Task<Dictionary<string, LanguageInfo>?> DiscoverLanguagesForAllBooks(
        string publicationCode,
        string publicationName,
        bool isTestRun)
    {
        var allDiscoveredLanguages = new Dictionary<string, LanguageInfo>(StringComparer.OrdinalIgnoreCase);

        Logger.Information("Discovering languages for all books (1-66) in publication {PublicationCode}", publicationCode);

        // Discover languages for each book (1-66)
        for (int bookNum = 1; bookNum <= 66; bookNum++)
        {
            try
            {
                var bookLanguages = await DiscoverLanguagesForBook(publicationCode, bookNum);

                if (bookLanguages != null && bookLanguages.Count > 0)
                {
                    // Filter out sign languages
                    bookLanguages = await signLanguageChecker.FilterSignLanguagesAsync(bookLanguages);

                    if (bookLanguages.Count > 0)
                    {
                        // Save section languages for this book
                        // The alllangs=1 response already lists only available languages, so no verification needed
                        if (dataPersister != null)
                        {
                            await dataPersister.SaveSectionLanguages(publicationCode, bookNum.ToString(), bookLanguages);
                        }

                        // Merge languages into consolidated dictionary
                        foreach (var lang in bookLanguages.Where(l => !allDiscoveredLanguages.ContainsKey(l.Key)))
                        {
                            allDiscoveredLanguages[lang.Key] = lang.Value;
                        }

                        Logger.Debug("Book {BookNum}: Discovered {LanguageCount} languages (total unique: {TotalLanguages})",
                            bookNum, bookLanguages.Count, allDiscoveredLanguages.Count);
                    }
                }
            }
            catch (System.Net.Http.HttpRequestException ex) when (ex.Message.Contains("Response status code"))
            {
                Logger.Warning("Book {BookNum}: HTTP error during language discovery. Skipping.", bookNum);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Book {BookNum}: Failed to discover languages. Skipping.", bookNum);
            }
        }

        if (allDiscoveredLanguages.Count == 0)
        {
            Logger.Warning("No languages discovered for publication {PublicationCode} across all books", publicationCode);
            return null;
        }

        Logger.Information("Total unique languages discovered across all books for {PublicationCode}: {LanguageCount}",
            publicationCode, allDiscoveredLanguages.Count);

        // Return discovered languages (alllangs=1 response already lists only available languages)
        return allDiscoveredLanguages;
    }

    /// <summary>
    /// Discovers languages for a specific book using alllangs=1 and langwritten=E.
    /// </summary>
    private async Task<Dictionary<string, LanguageInfo>?> DiscoverLanguagesForBook(string publicationCode, int bookNum)
    {
        var discoveredLanguages = new Dictionary<string, LanguageInfo>(StringComparer.OrdinalIgnoreCase);

        // Use alllangs=1 and langwritten=E for discovery
        var catalogLink = $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}?{AppConstants.Media.GetPubQueryOutputJson}&{AppConstants.Media.GetPubQueryParamName.Pub}={publicationCode}&{AppConstants.Media.GetPubQueryParamName.BookNum}={bookNum}&{AppConstants.Media.GetPubQueryParamName.FileFormat}={AppConstants.Media.MediaStreamFormatMp3}&{AppConstants.Media.GetPubQueryAllLangsOn}&{AppConstants.Media.GetPubQueryParamLangWritten}={AppConstants.Media.DefaultLanguageCode}";

        var jsonString = await DownloadUtility.GetAsync(catalogLink);
        using var doc = JsonDocument.Parse(jsonString);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
        {
            return discoveredLanguages;
        }

        if (!root.TryGetProperty(AppConstants.Media.LanguageIndexJson.Languages, out var languages))
        {
            return discoveredLanguages;
        }

        foreach (var item in languages.EnumerateObject())
        {
            // Normalize language code to uppercase for consistent storage
            var languageCode = item.Name.ToUpperInvariant();

            if (!item.Value.TryGetProperty(AppConstants.Media.PubMediaJson.Name, out var nameElement))
            {
                continue;
            }

            var rawLanguage = nameElement.GetString();
            // Decode HTML entities like &nbsp; to proper characters
            var language = Bible.Alarm.Shared.Helpers.MediaTrackTitleHelper.DecodeHtmlTitleNullable(rawLanguage);
            if (string.IsNullOrEmpty(language))
            {
                continue;
            }

            // Extract direction (defaults to ltr if not present)
            var direction = AppConstants.Media.TextDirectionLeftToRight;
            if (item.Value.TryGetProperty(AppConstants.Media.LanguageIndexJson.Direction, out var directionElement))
            {
                direction = directionElement.GetString() ?? AppConstants.Media.TextDirectionLeftToRight;
            }

            discoveredLanguages[languageCode] = new LanguageInfo(language, direction);
        }

        return discoveredLanguages;
    }

    /// <summary>
    /// Saves language discovery results for English (E) as JSON.
    /// Structure: media/Bible/E/{publicationCode}/language-discovery.json
    /// </summary>
    private async Task SaveLanguageDiscoveryForEnglish(string publicationCode, Dictionary<string, LanguageInfo> discoveredLanguages)
    {
        // Normalize publication code for path consistency
        var normalizedPublicationCode = publicationCode.ToUpperInvariant();
        var englishDir = $"{DirectoryHelper.IndexDirectory}/media/{AppConstants.Media.BiblePublicationCategoryBible}/{AppConstants.Media.DefaultLanguageCode}/{normalizedPublicationCode}";
        DirectoryHelper.Ensure(englishDir);

        var languageDiscoveryFile = Path.Combine(englishDir, "language-discovery.json");

        // Create a list of languages with Code, Name, and Direction
        var languageList = discoveredLanguages
            .Select(kvp => new
            {
                Code = kvp.Key,
                Name = kvp.Value.Name,
                Direction = kvp.Value.Direction
            })
            .OrderBy(x => x.Code)
            .ToList();

        var json = JsonSerializer.Serialize(languageList, IndentedJsonSerializerOptions);

        await File.WriteAllTextAsync(languageDiscoveryFile, json);

        Logger.Information("Saved language discovery for English: {LanguageCount} languages to {File}",
            discoveredLanguages.Count, languageDiscoveryFile);
    }
}

