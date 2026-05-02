#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.ViewModels.BiblePublications;
using Serilog;

namespace Bible.Alarm.ViewModels.Music.MusicSectionSelectionViewModelHelpers;

internal sealed class MusicInstrumentalSectionListLoader
{
    private readonly ILogger logger;
    private readonly IMediaService mediaService;

    public MusicInstrumentalSectionListLoader(ILogger logger, IMediaService mediaService)
    {
        this.logger = logger;
        this.mediaService = mediaService;
    }

    public async Task<(List<BiblePublicationSectionListViewItemModel> Items, BiblePublicationSectionListViewItemModel? Selected)> LoadAsync(
        string publicationCode,
        string? selectedSectionCode,
        IFetchProgress? progress = null)
    {
        var cancellationToken = progress?.CancellationToken ?? CancellationToken.None;

        var initialSections = await mediaService.GetSectionsForPublicationWithoutLanguage(publicationCode);

        var expectedSectionCount = await mediaService.GetExpectedSectionCountForNoLanguagePublicationAsync(publicationCode);
        var actualSectionCount = initialSections?.Values.Count ?? 0;
        var hasAllExpectedSections = actualSectionCount >= expectedSectionCount;
        var allSectionsCataloged = AreAllSectionsFullyCataloged(initialSections, expectedSectionCount);

        var prefetch = new InstrumentalSectionsPrefetchState(
            initialSections,
            expectedSectionCount,
            actualSectionCount,
            hasAllExpectedSections,
            allSectionsCataloged);

        var (items, selected) = await Task.Run(async () =>
            await ProcessInstrumentalSectionsBackgroundAsync(publicationCode, selectedSectionCode, progress, prefetch, cancellationToken));

        if (!prefetch.AllSectionsCataloged)
        {
            progress?.UpdateProgress(1.0);
            progress?.SetIsVisible(false);
        }

        return (items, selected);
    }

    private async Task<(List<BiblePublicationSectionListViewItemModel> Items, BiblePublicationSectionListViewItemModel? Selected)> ProcessInstrumentalSectionsBackgroundAsync(
        string publicationCode,
        string? selectedSectionCode,
        IFetchProgress? progress,
        InstrumentalSectionsPrefetchState prefetch,
        CancellationToken cancellationToken)
    {
        var sectionsFromDb = await ResolveSectionsFromPrefetchAsync(publicationCode, progress, prefetch, cancellationToken);

        if (sectionsFromDb == null || sectionsFromDb.Count == 0)
        {
            logger.Warning("[MusicSectionSelection] LoadAsync: No sections found for publication={PublicationCode}. This publication may not be cataloged yet or may not have sections.",
                publicationCode);

            return (new List<BiblePublicationSectionListViewItemModel>(), null);
        }

        RemoveInstrumentalPlaceholderSections(sectionsFromDb);

        if (sectionsFromDb.Count == 0)
        {
            return (new List<BiblePublicationSectionListViewItemModel>(), null);
        }

        var (items, selected) = BuildInstrumentalSectionViewModels(sectionsFromDb, selectedSectionCode);

        if (!prefetch.AllSectionsCataloged)
        {
            progress?.UpdateProgress(0.9);
        }

        return (items, selected);
    }

    private async Task<SortedDictionary<string, Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublicationSection>?> ResolveSectionsFromPrefetchAsync(
        string publicationCode,
        IFetchProgress? progress,
        InstrumentalSectionsPrefetchState prefetch,
        CancellationToken cancellationToken)
    {
        if (prefetch.AllSectionsCataloged)
        {
            logger.Debug("MusicInstrumentalSectionListLoader: All {ExpectedCount} expected sections already cataloged for publication={PublicationCode}, skipping fetch",
                prefetch.ExpectedSectionCount, publicationCode);
            return prefetch.InitialSections;
        }

        progress?.SetIsVisible(true);
        progress?.UpdateProgress(0.0);

        LogPrefetchFetchReason(publicationCode, prefetch);

        var sections =
            await RetryFetchUntilCatalogedAsync(publicationCode, progress, cancellationToken);

        progress?.UpdateProgress(0.7);
        return sections;
    }

    private void LogPrefetchFetchReason(string publicationCode, InstrumentalSectionsPrefetchState prefetch)
    {
        if (prefetch.HasAllExpectedSections)
        {
            logger.Debug("MusicInstrumentalSectionListLoader: Have {ActualCount} sections but some are placeholders, fetching remaining for publication={PublicationCode}",
                prefetch.ActualSectionCount, publicationCode);
            return;
        }

        logger.Debug("MusicInstrumentalSectionListLoader: Only {ActualCount}/{ExpectedCount} sections found, fetching remaining for publication={PublicationCode}",
            prefetch.ActualSectionCount, prefetch.ExpectedSectionCount, publicationCode);
    }

    private static (List<BiblePublicationSectionListViewItemModel> Items, BiblePublicationSectionListViewItemModel? Selected) BuildInstrumentalSectionViewModels(
        SortedDictionary<string, Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublicationSection> sectionsFromDb,
        string? selectedSectionCode)
    {
        var items = new List<BiblePublicationSectionListViewItemModel>();
        BiblePublicationSectionListViewItemModel? selected = null;

        foreach (var section in sectionsFromDb.Values)
        {
            var sectionVm = new BiblePublicationSectionListViewItemModel(section);
            items.Add(sectionVm);

            if (!string.IsNullOrEmpty(selectedSectionCode) &&
                section.SectionCode.Equals(selectedSectionCode, StringComparison.OrdinalIgnoreCase))
            {
                selected = sectionVm;
                selected.IsSelected = true;
            }
        }

        items.Sort();
        return (items, selected);
    }

    private void RemoveInstrumentalPlaceholderSections(
        SortedDictionary<string, Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublicationSection> sectionsFromDb)
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

        logger.Information("MusicInstrumentalSectionListLoader: Removed {Count} unfetchable placeholder sections: {Codes}",
            unfetchableSectionCodes.Count, string.Join(", ", unfetchableSectionCodes));
    }

    private readonly record struct InstrumentalSectionsPrefetchState(
        SortedDictionary<string, Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublicationSection>? InitialSections,
        int ExpectedSectionCount,
        int ActualSectionCount,
        bool HasAllExpectedSections,
        bool AllSectionsCataloged);

    private async Task<SortedDictionary<string, Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublicationSection>?> RetryFetchUntilCatalogedAsync(
        string publicationCode,
        IFetchProgress? progress,
        CancellationToken cancellationToken)
    {
        logger.Information("MusicInstrumentalSectionListLoader: Starting fetch with retries for publication={PublicationCode}",
            publicationCode);

        progress?.SetIsVisible(true);
        progress?.UpdateProgress(0.0);

        var allCataloged = false;
        var attempt = 0;
        SortedDictionary<string, Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublicationSection>? sectionsData = null;

        try
        {
            var loopOutcome = await RunInstrumentalCatalogRetryLoopAsync(publicationCode, retryDelay: 1000, cancellationToken);
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
            logger.Warning("MusicInstrumentalSectionListLoader: Timeout after {Attempts} attempts waiting for all sections to be cataloged for publication {PublicationCode}. Some may still be placeholders.",
                attempt, publicationCode);

            sectionsData = await TryRecoverInstrumentalSectionsAfterTimeoutAsync(publicationCode, sectionsData);
        }

        return sectionsData;
    }

    private sealed class InstrumentalCatalogRetryLoopOutcome
    {
        public bool AllCataloged { get; init; }
        public int Attempt { get; init; }
        public SortedDictionary<string, Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublicationSection>? SectionsData { get; init; }
    }

    private async Task<InstrumentalCatalogRetryLoopOutcome> RunInstrumentalCatalogRetryLoopAsync(
        string publicationCode,
        int retryDelay,
        CancellationToken cancellationToken)
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
                var iterationOutcome = await RunInstrumentalCatalogRetryIterationAsync(
                    publicationCode,
                    attempt,
                    retryDelay,
                    previousCatalogedCount,
                    cancellationToken);

                if (ApplyInstrumentalCatalogIterationOutcome(
                        iterationOutcome,
                        ref sectionsData,
                        ref allCataloged,
                        ref previousCatalogedCount,
                        publicationCode,
                        attempt))
                {
                    break;
                }
            }
            catch (Exception ex)
            {
                await HandleInstrumentalCatalogRetryExceptionAsync(
                    ex, attempt, publicationCode, retryDelay, cancellationToken);
            }
        }

        return new InstrumentalCatalogRetryLoopOutcome
        {
            AllCataloged = allCataloged,
            Attempt = attempt,
            SectionsData = sectionsData
        };
    }

    private bool ApplyInstrumentalCatalogIterationOutcome(
        InstrumentalCatalogRetryIterationOutcome iterationOutcome,
        ref SortedDictionary<string, Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublicationSection>? sectionsData,
        ref bool allCataloged,
        ref int previousCatalogedCount,
        string publicationCode,
        int attempt)
    {
        sectionsData = iterationOutcome.NextSectionsSnapshot;
        if (iterationOutcome.Completed)
        {
            allCataloged = true;
            if (iterationOutcome.LogSuccess)
            {
                logger.Information("MusicInstrumentalSectionListLoader: All {ExpectedCount} expected sections cataloged on attempt {Attempt} for publication={PublicationCode}",
                    iterationOutcome.ExpectedSectionCount, attempt, publicationCode);
            }

            return false;
        }

        if (iterationOutcome.BreakRetries)
        {
            return true;
        }

        previousCatalogedCount = iterationOutcome.UpdatedPreviousCatalogedCount;
        return false;
    }

    private async Task HandleInstrumentalCatalogRetryExceptionAsync(
        Exception ex,
        int attempt,
        string publicationCode,
        int retryDelay,
        CancellationToken cancellationToken)
    {
        if (NetworkExceptionHelper.ShouldRethrowFromCatalogRetryLoop(ex))
        {
            ExceptionDispatchInfo.Capture(ex).Throw();
        }

        logger.Warning(ex, "MusicInstrumentalSectionListLoader: Attempt {Attempt} failed for publication={PublicationCode}, will retry",
            attempt, publicationCode);

        var delay = Math.Min(retryDelay * attempt, 5000);
        await Task.Delay(delay, cancellationToken);
    }

    private async Task<SortedDictionary<string, Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublicationSection>?> TryRecoverInstrumentalSectionsAfterTimeoutAsync(
        string publicationCode,
        SortedDictionary<string, Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublicationSection>? sectionsData)
    {
        if (sectionsData != null && sectionsData.Count > 0)
        {
            return sectionsData;
        }

        try
        {
            return await mediaService.GetSectionsForPublicationWithoutLanguage(publicationCode);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "MusicInstrumentalSectionListLoader: Final fetch attempt failed for publication={PublicationCode}",
                publicationCode);
            return sectionsData;
        }
    }

    private async Task<InstrumentalCatalogRetryIterationOutcome> RunInstrumentalCatalogRetryIterationAsync(
        string publicationCode,
        int attempt,
        int retryDelayBase,
        int previousCatalogedCount,
        CancellationToken cancellationToken)
    {
        var fetchedWithProgress =
            await mediaService.GetSectionsForPublicationWithoutLanguage(publicationCode);

        await Task.Delay(500, cancellationToken);

        var reQueriedData = await mediaService.GetSectionsForPublicationWithoutLanguage(publicationCode);

        var expectedSectionCount = await mediaService.GetExpectedSectionCountForNoLanguagePublicationAsync(publicationCode);
        var actualSectionCount = reQueriedData?.Values.Count ?? 0;

        var hasAllExpectedSections = actualSectionCount >= expectedSectionCount;
        if (AreAllSectionsFullyCataloged(reQueriedData, expectedSectionCount))
        {
            return InstrumentalCatalogRetryIterationOutcome.ForSuccess(reQueriedData!, expectedSectionCount);
        }

        var currentCatalogedCount =
            reQueriedData?.Values.Count(static s =>
                !string.IsNullOrEmpty(s.Name) && s.Id > 0) ?? 0;

        if (currentCatalogedCount > 0 && currentCatalogedCount <= previousCatalogedCount)
        {
            logger.Information("MusicInstrumentalSectionListLoader: No progress between retries ({CatalogedCount} cataloged, {ExpectedCount} expected). Remaining placeholders are unfetchable. Stopping retries for publication={PublicationCode}",
                currentCatalogedCount, expectedSectionCount, publicationCode);
            return InstrumentalCatalogRetryIterationOutcome.ForStagnation(reQueriedData);
        }

        LogRetryIterationDiagnostics(attempt, reQueriedData, hasAllExpectedSections, actualSectionCount, expectedSectionCount);

        var delay = Math.Min(retryDelayBase * attempt, 5000);
        await Task.Delay(delay, cancellationToken);

        return InstrumentalCatalogRetryIterationOutcome.ForContinue(fetchedWithProgress, currentCatalogedCount);
    }

    private sealed record InstrumentalCatalogRetryIterationOutcome(
        bool Completed,
        bool LogSuccess,
        bool BreakRetries,
        SortedDictionary<string, Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublicationSection>? NextSectionsSnapshot,
        int ExpectedSectionCount,
        int UpdatedPreviousCatalogedCount)
    {
        public static InstrumentalCatalogRetryIterationOutcome ForSuccess(
            SortedDictionary<string, Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublicationSection> reQueried,
            int expectedSectionCount) =>
            new(true, LogSuccess: true, BreakRetries: false, NextSectionsSnapshot: reQueried, ExpectedSectionCount: expectedSectionCount, UpdatedPreviousCatalogedCount: -1);

        public static InstrumentalCatalogRetryIterationOutcome ForStagnation(
            SortedDictionary<string, Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublicationSection>? reQueried) =>
            new(false, LogSuccess: false, BreakRetries: true, NextSectionsSnapshot: reQueried, ExpectedSectionCount: 0, UpdatedPreviousCatalogedCount: -1);

        public static InstrumentalCatalogRetryIterationOutcome ForContinue(
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

        if (sections == null || sections.Count == 0)
        {
            return false;
        }

        return sections.Values.All(static s =>
            !string.IsNullOrEmpty(s.Name) &&
            s.Id > 0);
    }

    private void LogRetryIterationDiagnostics(
        int attempt,
        SortedDictionary<string, Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublicationSection>? reQueriedData,
        bool hasAllExpectedSections,
        int actualSectionCount,
        int expectedSectionCount)
    {
        if (reQueriedData != null)
        {
            var placeholders = reQueriedData.Values.Where(static s =>
                    string.IsNullOrEmpty(s.Name) ||
                    s.Id == 0)
                .Select(s => s.SectionCode)
                .ToList();

            if (placeholders.Count > 0)
            {
                logger.Debug("MusicInstrumentalSectionListLoader: Attempt {Attempt}: Still waiting for {Count} sections to be cataloged: {Placeholders}",
                    attempt, placeholders.Count, string.Join(", ", placeholders));
                return;
            }

            if (!hasAllExpectedSections)
            {
                logger.Debug("MusicInstrumentalSectionListLoader: Attempt {Attempt}: Only {ActualCount}/{ExpectedCount} sections found, will retry",
                    attempt, actualSectionCount, expectedSectionCount);
                return;
            }
        }

        logger.Debug("MusicInstrumentalSectionListLoader: Attempt {Attempt}: No sections found yet, will retry",
            attempt);
    }
}
