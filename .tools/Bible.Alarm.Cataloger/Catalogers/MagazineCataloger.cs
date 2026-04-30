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
using Bible.Alarm.Shared.Helpers;
using Serilog;

namespace Bible.Alarm.Cataloger.Catalogers;

/// <summary>
/// Cataloger for magazine publications (Watchtower and Awake!).
/// Handles discovery of available issues and their languages.
/// </summary>
internal sealed class MagazineCataloger : BaseCataloger
{
    private const int MaxConcurrentApiCalls = 6;
    private readonly IDataPersister? dataPersister;
    private readonly SignLanguageChecker signLanguageChecker;

    public MagazineCataloger(ILogger logger, DownloadUtility downloadUtility, IDataPersister? dataPersister)
        : base(logger, downloadUtility)
    {
        this.dataPersister = dataPersister;
        signLanguageChecker = new SignLanguageChecker(logger, downloadUtility);
    }

    /// <summary>
    /// Discovers all languages for all magazine issues across all years.
    /// For each issue, calls the API with alllangs=1 to discover available languages.
    /// Populates SectionLanguages and PublicationLanguages via dataPersister.
    /// </summary>
    internal async Task DiscoverLanguagesForMagazines(bool isTestRun = false)
    {
        Logger.Information("=== MAGAZINE DISCOVERY: Discovering languages for Watchtower and Awake! magazines ===");

        await DiscoverLanguagesForMagazineType(isWatchtower: true, isTestRun);
        await DiscoverLanguagesForMagazineType(isWatchtower: false, isTestRun);

        Logger.Information("=== MAGAZINE DISCOVERY COMPLETED ===");
    }

    private async Task DiscoverLanguagesForMagazineType(bool isWatchtower, bool isTestRun)
    {
        var magazineName = isWatchtower ? "Watchtower" : "Awake!";
        var pubCodePrefix = isWatchtower ? "w" : "g";

        Logger.Information("Discovering languages for {MagazineName} ({StartYear}-{EndYear})",
            magazineName, MagazineHelper.MagazineStartYear, MagazineHelper.MagazineEndYear);

        var semaphore = new SemaphoreSlim(MaxConcurrentApiCalls);
        var tasks = new List<Task<YearDiscoveryResult>>();

        for (int year = MagazineHelper.MagazineStartYear; year <= MagazineHelper.MagazineEndYear; year++)
        {
            var capturedYear = year;
            tasks.Add(DiscoverLanguagesForYear(capturedYear, isWatchtower, pubCodePrefix, semaphore, isTestRun));
        }

        var results = await Task.WhenAll(tasks);

        var totalIssues = results.Sum(r => r.DiscoveredIssueCount);
        var yearsWithIssues = results.Count(r => r.DiscoveredIssueCount > 0);
        Logger.Information("{MagazineName}: Discovered {TotalIssues} issues across {YearsWithIssues} years",
            magazineName, totalIssues, yearsWithIssues);
    }

    private async Task<YearDiscoveryResult> DiscoverLanguagesForYear(
        int year, bool isWatchtower, string pubCodePrefix,
        SemaphoreSlim semaphore, bool isTestRun)
    {
        var pubCode = $"{pubCodePrefix}{year}";
        var possibleIssues = MagazineHelper.GetPossibleIssues(year, isWatchtower);
        var mergedLanguages = new Dictionary<string, LanguageInfo>(StringComparer.OrdinalIgnoreCase);
        int discoveredIssueCount = 0;

        foreach (var (apiPubCode, issueCode) in possibleIssues)
        {
            await semaphore.WaitAsync();
            try
            {
                var issueLanguages = await DiscoverLanguagesForIssue(apiPubCode, issueCode, isTestRun);
                if (issueLanguages == null || issueLanguages.Count == 0)
                {
                    continue;
                }

                discoveredIssueCount++;
                var sectionCode = MagazineHelper.BuildSectionCode(apiPubCode, issueCode);

                if (dataPersister != null)
                {
                    await dataPersister.SaveSectionLanguages(pubCode, sectionCode, issueLanguages);
                }

                foreach (var lang in issueLanguages.Where(l => !mergedLanguages.ContainsKey(l.Key)))
                {
                    mergedLanguages[lang.Key] = lang.Value;
                }
            }
            finally
            {
                semaphore.Release();
            }
        }

        if (discoveredIssueCount > 0 && mergedLanguages.Count > 0 && dataPersister != null)
        {
            await dataPersister.SavePublicationLanguages(pubCode, mergedLanguages);

            var languageCodeToNameMapping = mergedLanguages.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Name, StringComparer.OrdinalIgnoreCase);
            await dataPersister.SaveLanguageDiscovery("E", pubCode, languageCodeToNameMapping);

            Logger.Information("Year {Year} ({PubCode}): {IssueCount} issues, {LanguageCount} languages",
                year, pubCode, discoveredIssueCount, mergedLanguages.Count);
        }
        else if (discoveredIssueCount == 0)
        {
            Logger.Debug("Year {Year} ({PubCode}): No issues found", year, pubCode);
        }

        return new YearDiscoveryResult(year, discoveredIssueCount);
    }

    private async Task<Dictionary<string, LanguageInfo>?> DiscoverLanguagesForIssue(
        string apiPubCode, string issueCode, bool isTestRun)
    {
        var url = $"{AppConstants.ApiEndpoints.JwOrgIndexServiceBaseUrl}" +
                  $"?{AppConstants.Media.GetPubQueryOutputJson}&{AppConstants.Media.GetPubQueryParamName.Pub}={apiPubCode}&{AppConstants.Media.GetPubQueryParamName.Issue}={issueCode}&{AppConstants.Media.GetPubQueryParamName.FileFormat}={AppConstants.Media.MediaStreamFormatMp3}&{AppConstants.Media.GetPubQueryAllLangsOn}&{AppConstants.Media.GetPubQueryParamLangWritten}={AppConstants.Media.DefaultLanguageCode}";

        try
        {
            var jsonString = await DownloadUtility.GetAsync(url);
            var discoveredLanguages = ParseLanguagesFromResponse(jsonString);

            if (discoveredLanguages == null || discoveredLanguages.Count == 0)
            {
                return null;
            }

            discoveredLanguages = await signLanguageChecker.FilterSignLanguagesAsync(discoveredLanguages);

            if (isTestRun)
            {
                var filtered = new Dictionary<string, LanguageInfo>(StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in discoveredLanguages.Where(k => TestRunLanguageCodes.Contains(k.Key)))
                {
                    filtered[kvp.Key] = kvp.Value;
                }
                return filtered.Count > 0 ? filtered : null;
            }

            return discoveredLanguages;
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("Response status code", StringComparison.Ordinal))
        {
            Logger.Debug(ex, "Issue {ApiPub}/{IssueCode}: not available (HTTP error)", apiPubCode, issueCode);
            return null;
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Issue {ApiPub}/{IssueCode}: failed to discover languages", apiPubCode, issueCode);
            return null;
        }
    }

    private static Dictionary<string, LanguageInfo> ParseLanguagesFromResponse(string jsonString)
    {
        var result = new Dictionary<string, LanguageInfo>(StringComparer.OrdinalIgnoreCase);

        using var doc = JsonDocument.Parse(jsonString);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(AppConstants.Media.LanguageIndexJson.Languages, out var languages))
        {
            return result;
        }

        if (languages.ValueKind == JsonValueKind.Object)
        {
            foreach (var item in languages.EnumerateObject())
            {
                var languageCode = item.Name.ToUpperInvariant();

                if (!item.Value.TryGetProperty(AppConstants.Media.PubMediaJson.Name, out var nameElement))
                    continue;

                var rawName = nameElement.GetString();
                var name = MediaTrackTitleHelper.DecodeHtmlTitleNullable(rawName);
                if (string.IsNullOrEmpty(name))
                    continue;

                var direction = AppConstants.Media.TextDirectionLeftToRight;
                if (item.Value.TryGetProperty(AppConstants.Media.LanguageIndexJson.Direction, out var dirElement))
                {
                    direction = dirElement.GetString() ?? AppConstants.Media.TextDirectionLeftToRight;
                }

                result[languageCode] = new LanguageInfo(name, direction);
            }
        }
        else if (languages.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in languages.EnumerateArray())
            {
                string? languageCode = null;
                if (element.TryGetProperty(AppConstants.Media.LanguageIndexJson.LangCode, out var lc))
                {
                    languageCode = lc.GetString()?.ToUpperInvariant();
                }
                else if (element.TryGetProperty(AppConstants.Media.LanguageIndexJson.Symbol, out var sym))
                {
                    languageCode = sym.GetString()?.ToUpperInvariant();
                }
                if (string.IsNullOrEmpty(languageCode))
                    continue;

                if (!element.TryGetProperty(AppConstants.Media.PubMediaJson.Name, out var nameElement))
                    continue;

                var rawName = nameElement.GetString();
                var name = MediaTrackTitleHelper.DecodeHtmlTitleNullable(rawName);
                if (string.IsNullOrEmpty(name))
                    continue;

                var direction = AppConstants.Media.TextDirectionLeftToRight;
                if (element.TryGetProperty(AppConstants.Media.LanguageIndexJson.Direction, out var dirElement))
                {
                    direction = dirElement.GetString() ?? AppConstants.Media.TextDirectionLeftToRight;
                }

                result[languageCode] = new LanguageInfo(name, direction);
            }
        }

        return result;
    }

    private sealed record YearDiscoveryResult(int Year, int DiscoveredIssueCount);
}
