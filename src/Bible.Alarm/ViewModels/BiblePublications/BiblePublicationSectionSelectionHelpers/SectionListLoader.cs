#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Serilog;

namespace Bible.Alarm.ViewModels.BiblePublications.BiblePublicationSectionSelectionHelpers;

internal sealed class SectionListLoader
{
    private readonly ILogger logger;
    private readonly IMediaService mediaService;
    private readonly IInternetConnectivityChecker? internetChecker;

    public SectionListLoader(ILogger logger, IMediaService mediaService, IInternetConnectivityChecker? internetChecker = null)
    {
        this.logger = logger;
        this.mediaService = mediaService;
        this.internetChecker = internetChecker;
    }

    internal async Task<(List<BiblePublicationSectionListViewItemModel> Items, Dictionary<string, BiblePublicationSectionListViewItemModel> Mapping)> LoadAsync(
        string languageCode,
        string publicationCode,
        string? selectedSectionCode,
        IFetchProgress? progress = null)
    {
        var cancellationToken = progress?.CancellationToken ?? CancellationToken.None;
        var sectionsFromDb = await ResolveSectionsFromSourcesAsync(languageCode, publicationCode, progress, cancellationToken);

        progress?.UpdateProgress(0.7);

        if (sectionsFromDb == null || sectionsFromDb.Count == 0)
        {
            LogNoSectionsWarning(publicationCode, languageCode);
            progress?.UpdateProgress(1.0);
            progress?.SetIsVisible(false);
            return EmptySectionResult();
        }

        RemovePlaceholderSections(sectionsFromDb);

        if (sectionsFromDb.Count == 0)
        {
            progress?.UpdateProgress(1.0);
            progress?.SetIsVisible(false);
            return EmptySectionResult();
        }

        var (vms, map) = BuildSortedSectionViewModels(sectionsFromDb, selectedSectionCode);

        progress?.UpdateProgress(0.9);
        progress?.UpdateProgress(1.0);
        progress?.SetIsVisible(false);

        return (vms, map);
    }

    private async Task<SortedDictionary<string, Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublicationSection>?> ResolveSectionsFromSourcesAsync(
        string languageCode,
        string publicationCode,
        IFetchProgress? progress,
        CancellationToken cancellationToken)
    {
        var isNonEnglish = !string.IsNullOrEmpty(languageCode) &&
                           !languageCode.Equals(AppConstants.Media.DefaultLanguageCode, StringComparison.OrdinalIgnoreCase);
        if (!isNonEnglish)
        {
            return await mediaService.GetBiblePublicationSections(languageCode, publicationCode, progress);
        }

        return await LoadNonEnglishSectionsWithOptionalRetryAsync(languageCode, publicationCode, progress, cancellationToken);
    }

    private async Task<SortedDictionary<string, Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublicationSection>?> LoadNonEnglishSectionsWithOptionalRetryAsync(
        string languageCode,
        string publicationCode,
        IFetchProgress? progress,
        CancellationToken cancellationToken)
    {
        var initialSections = await mediaService.GetBiblePublicationSections(languageCode, publicationCode, null);
        var expectedSectionCount = await mediaService.GetExpectedSectionCountAsync(languageCode, publicationCode);
        var actualSectionCount = initialSections?.Values.Count ?? 0;
        var hasAllExpectedSections = actualSectionCount >= expectedSectionCount;

        if (AreAllSectionsFullyCataloged(initialSections, expectedSectionCount))
        {
            logger.Debug("SectionListLoader: All {ExpectedCount} expected sections already cataloged for publication={PublicationCode}, language={LanguageCode}, skipping fetch",
                expectedSectionCount, publicationCode, languageCode);
            return initialSections;
        }

        progress?.SetIsVisible(true);
        progress?.UpdateProgress(0.0);

        LogNonEnglishSectionFetchReason(hasAllExpectedSections, actualSectionCount, expectedSectionCount, publicationCode, languageCode);
        return await RetryFetchUntilCatalogedAsync(languageCode, publicationCode, progress, cancellationToken);
    }

    private void LogNonEnglishSectionFetchReason(
        bool hasAllExpectedSections,
        int actualSectionCount,
        int expectedSectionCount,
        string publicationCode,
        string languageCode)
    {
        if (hasAllExpectedSections)
        {
            logger.Debug("SectionListLoader: Have {ActualCount} sections but some are placeholders, fetching remaining sections for publication={PublicationCode}, language={LanguageCode}",
                actualSectionCount, publicationCode, languageCode);
            return;
        }

        logger.Debug("SectionListLoader: Only {ActualCount}/{ExpectedCount} sections found, fetching remaining sections for publication={PublicationCode}, language={LanguageCode}",
            actualSectionCount, expectedSectionCount, publicationCode, languageCode);
    }

    private static (List<BiblePublicationSectionListViewItemModel> Items, Dictionary<string, BiblePublicationSectionListViewItemModel> Map) EmptySectionResult() =>
        (new List<BiblePublicationSectionListViewItemModel>(), new Dictionary<string, BiblePublicationSectionListViewItemModel>(StringComparer.OrdinalIgnoreCase));

    private void LogNoSectionsWarning(string publicationCode, string? languageCode)
    {
        logger.Warning(
            "SectionListLoader: No sections found for publication={PublicationCode}, language={LanguageCode}. This publication may not be cataloged yet or may not have sections.",
            publicationCode,
            languageCode ?? "(null)");
    }

    private void RemovePlaceholderSections(SortedDictionary<string, Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublicationSection> sectionsFromDb)
    {
        var unfetchableSectionCodes = sectionsFromDb.Values
            .Where(s => s.Id == 0 || string.IsNullOrEmpty(s.Name))
            .Select(s => s.SectionCode)
            .ToList();
        if (unfetchableSectionCodes.Count == 0)
        {
            return;
        }

        foreach (var code in unfetchableSectionCodes)
        {
            sectionsFromDb.Remove(code);
        }

        logger.Information("SectionListLoader: Removed {Count} unfetchable placeholder sections: {Codes}",
            unfetchableSectionCodes.Count, string.Join(", ", unfetchableSectionCodes));
    }

    private static (List<BiblePublicationSectionListViewItemModel> Vms, Dictionary<string, BiblePublicationSectionListViewItemModel> Map) BuildSortedSectionViewModels(
        SortedDictionary<string, Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublicationSection> sectionsFromDb,
        string? selectedSectionCode)
    {
        var vms = new List<BiblePublicationSectionListViewItemModel>();
        var map = new Dictionary<string, BiblePublicationSectionListViewItemModel>(StringComparer.OrdinalIgnoreCase);

        foreach (var section in sectionsFromDb.Values)
        {
            var sectionVm = new BiblePublicationSectionListViewItemModel(section);
            vms.Add(sectionVm);
            map[section.SectionCode] = sectionVm;

            if (!string.IsNullOrEmpty(selectedSectionCode) &&
                string.Equals(section.SectionCode, selectedSectionCode, StringComparison.OrdinalIgnoreCase))
            {
                sectionVm.IsSelected = true;
            }
        }

        vms.Sort();
        return (vms, map);
    }

    private async Task<SortedDictionary<string, Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublicationSection>?> RetryFetchUntilCatalogedAsync(
        string languageCode,
        string publicationCode,
        IFetchProgress? progress,
        CancellationToken cancellationToken)
    {
        var retryDelay = 1000;

        logger.Information("SectionListLoader: Starting fetch with retries for publication={PublicationCode}, language={LanguageCode}",
            publicationCode, languageCode);

        progress?.SetIsVisible(true);
        progress?.UpdateProgress(0.0);

        await NetworkExceptionHelper.ThrowIfNoInternetAsync(internetChecker);

        var allCataloged = false;
        var attempt = 0;
        SortedDictionary<string, Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublicationSection>? sectionsData = null;

        try
        {
            var loopOutcome = await RunBibleSectionCatalogRetryLoopAsync(
                languageCode,
                publicationCode,
                progress,
                cancellationToken,
                retryDelay);
            sectionsData = loopOutcome.SectionsData;
            allCataloged = loopOutcome.AllCataloged;
            attempt = loopOutcome.Attempt;
        }
        finally
        {
            progress?.SetIsVisible(false);
        }

        if (!allCataloged)
        {
            logger.Warning("SectionListLoader: Timeout after {Attempts} attempts waiting for all sections to be cataloged for publication {PublicationCode}, language {LanguageCode}. Some may still be placeholders.",
                attempt, publicationCode, languageCode);

            sectionsData = await TryRecoverSectionsAfterCatalogRetryTimeoutAsync(languageCode, publicationCode, sectionsData);
        }

        return sectionsData;
    }

    private sealed class BibleSectionCatalogRetryLoopOutcome
    {
        public bool AllCataloged { get; init; }
        public int Attempt { get; init; }
        public SortedDictionary<string, Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublicationSection>? SectionsData { get; init; }
    }

    private async Task<BibleSectionCatalogRetryLoopOutcome> RunBibleSectionCatalogRetryLoopAsync(
        string languageCode,
        string publicationCode,
        IFetchProgress? progress,
        CancellationToken cancellationToken,
        int retryDelay)
    {
        const int maxRetries = 10;
        var maxWaitTime = TimeSpan.FromSeconds(60);
        var startTime = DateTime.UtcNow;
        var allCataloged = false;
        var attempt = 0;
        var previousCatalogedCount = -1;
        SortedDictionary<string, Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublicationSection>? sectionsData = null;

        while (!allCataloged && attempt < maxRetries && (DateTime.UtcNow - startTime) < maxWaitTime)
        {
            cancellationToken.ThrowIfCancellationRequested();

            attempt++;

            try
            {
                var iterationOutcome = await RunCatalogRetryIterationAsync(
                    languageCode,
                    publicationCode,
                    progress,
                    cancellationToken,
                    attempt,
                    retryDelay,
                    previousCatalogedCount);

                sectionsData = iterationOutcome.NextSectionsSnapshot;
                if (iterationOutcome.Completed)
                {
                    allCataloged = true;
                    if (iterationOutcome.LogSuccess)
                    {
                        logger.Information("SectionListLoader: All {ExpectedCount} expected sections cataloged on attempt {Attempt} for publication={PublicationCode}, language={LanguageCode}",
                            iterationOutcome.ExpectedSectionCount, attempt, publicationCode, languageCode);
                    }
                }
                else if (iterationOutcome.BreakRetries)
                {
                    break;
                }
                else
                {
                    previousCatalogedCount = iterationOutcome.UpdatedPreviousCatalogedCount;
                }
            }
            catch (Exception ex)
            {
                if (NetworkExceptionHelper.ShouldRethrowFromCatalogRetryLoop(ex))
                {
                    throw;
                }

                logger.Warning(ex, "SectionListLoader: Attempt {Attempt} failed for publication={PublicationCode}, language={LanguageCode}, will retry",
                    attempt, publicationCode, languageCode);

                var delay = Math.Min(retryDelay * attempt, 5000);
                await Task.Delay(delay, cancellationToken);
            }
        }

        return new BibleSectionCatalogRetryLoopOutcome
        {
            AllCataloged = allCataloged,
            Attempt = attempt,
            SectionsData = sectionsData
        };
    }

    private async Task<SortedDictionary<string, Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublicationSection>?> TryRecoverSectionsAfterCatalogRetryTimeoutAsync(
        string languageCode,
        string publicationCode,
        SortedDictionary<string, Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublicationSection>? sectionsData)
    {
        if (sectionsData != null && sectionsData.Count > 0)
        {
            return sectionsData;
        }

        try
        {
            return await mediaService.GetBiblePublicationSections(languageCode, publicationCode, null);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "SectionListLoader: Final fetch attempt failed for publication={PublicationCode}, language={LanguageCode}",
                publicationCode, languageCode);
            return sectionsData;
        }
    }

    private async Task<CatalogRetryIterationOutcome> RunCatalogRetryIterationAsync(
        string languageCode,
        string publicationCode,
        IFetchProgress? progress,
        CancellationToken cancellationToken,
        int attempt,
        int retryDelayBase,
        int previousCatalogedCount)
    {
        var fetchedWithProgress =
            await mediaService.GetBiblePublicationSections(languageCode, publicationCode, progress);

        await Task.Delay(500, cancellationToken);

        var reQueriedData = await mediaService.GetBiblePublicationSections(languageCode, publicationCode, null);

        var expectedSectionCount = await mediaService.GetExpectedSectionCountAsync(languageCode, publicationCode);
        var actualSectionCount = reQueriedData?.Values.Count ?? 0;

        var hasAllExpectedSections = actualSectionCount >= expectedSectionCount;
        if (AreAllSectionsFullyCataloged(reQueriedData, expectedSectionCount))
        {
            return CatalogRetryIterationOutcome.ForSuccess(reQueriedData!, expectedSectionCount);
        }

        var currentCatalogedCount =
            reQueriedData?.Values.Count(static s =>
                !string.IsNullOrEmpty(s.Name) && s.Id > 0) ?? 0;

        if (currentCatalogedCount > 0 && currentCatalogedCount <= previousCatalogedCount)
        {
            logger.Information("SectionListLoader: No progress between retries ({CatalogedCount} cataloged, {ExpectedCount} expected). Remaining placeholders are unfetchable. Stopping retries for publication={PublicationCode}, language={LanguageCode}",
                currentCatalogedCount, expectedSectionCount, publicationCode, languageCode);
            return CatalogRetryIterationOutcome.ForStagnation(reQueriedData);
        }

        LogSectionRetryIterationDiagnostics(attempt, reQueriedData, hasAllExpectedSections, actualSectionCount, expectedSectionCount);

        var delay = Math.Min(retryDelayBase * attempt, 5000);
        await Task.Delay(delay, cancellationToken);

        return CatalogRetryIterationOutcome.ForContinue(fetchedWithProgress, currentCatalogedCount);
    }

    private sealed record CatalogRetryIterationOutcome(
        bool Completed,
        bool LogSuccess,
        bool BreakRetries,
        SortedDictionary<string, Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublicationSection>? NextSectionsSnapshot,
        int ExpectedSectionCount,
        int UpdatedPreviousCatalogedCount)
    {
        public static CatalogRetryIterationOutcome ForSuccess(
            SortedDictionary<string, Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublicationSection> reQueried,
            int expectedSectionCount) =>
            new(true, LogSuccess: true, BreakRetries: false, NextSectionsSnapshot: reQueried, ExpectedSectionCount: expectedSectionCount, UpdatedPreviousCatalogedCount: -1);

        public static CatalogRetryIterationOutcome ForStagnation(
            SortedDictionary<string, Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublicationSection>? reQueried) =>
            new(false, LogSuccess: false, BreakRetries: true, NextSectionsSnapshot: reQueried, ExpectedSectionCount: 0, UpdatedPreviousCatalogedCount: -1);

        public static CatalogRetryIterationOutcome ForContinue(
            SortedDictionary<string, Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublicationSection>? fetchedWithProgress,
            int updatedPreviousCatalogedCount) =>
            new(false, LogSuccess: false, BreakRetries: false, NextSectionsSnapshot: fetchedWithProgress, ExpectedSectionCount: 0, UpdatedPreviousCatalogedCount: updatedPreviousCatalogedCount);
    }

    private static bool AreAllSectionsFullyCataloged(
        SortedDictionary<string, Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublicationSection>? sections,
        int expectedSectionCount)
    {
        var actualSectionCount = sections?.Values.Count ?? 0;
        if (actualSectionCount < expectedSectionCount)
        {
            return false;
        }

        if (sections == null || sections.Values.Count == 0)
        {
            return false;
        }

        return sections.Values.All(s =>
            !string.IsNullOrEmpty(s.Name) &&
            s.Id > 0);
    }

    private void LogSectionRetryIterationDiagnostics(
        int attempt,
        SortedDictionary<string, Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublicationSection>? reQueriedData,
        bool hasAllExpectedSections,
        int actualSectionCount,
        int expectedSectionCount)
    {
        if (reQueriedData == null)
        {
            return;
        }

        var placeholders = reQueriedData.Values.Where(s =>
                string.IsNullOrEmpty(s.Name) ||
                s.Id == 0)
            .Select(s => s.SectionCode)
            .ToList();

        if (placeholders.Count > 0)
        {
            logger.Debug("SectionListLoader: Attempt {Attempt}: Still waiting for {Count} sections to be cataloged: {Placeholders}",
                attempt, placeholders.Count, string.Join(", ", placeholders));
            return;
        }

        if (!hasAllExpectedSections)
        {
            logger.Debug("SectionListLoader: Attempt {Attempt}: Only {ActualCount}/{ExpectedCount} sections found, will retry",
                attempt, actualSectionCount, expectedSectionCount);
            return;
        }

        if (actualSectionCount == 0)
        {
            logger.Debug("SectionListLoader: Attempt {Attempt}: No sections found yet, will retry",
                attempt);
        }
    }
}

