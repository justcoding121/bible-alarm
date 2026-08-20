#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.ViewModels.BiblePublications;
using Serilog;
using SectionMap = System.Collections.Generic.SortedDictionary<string, Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublicationSection>;

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

    private async Task<SectionMap?> ResolveSectionsFromPrefetchAsync(
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
        SectionMap sectionsFromDb,
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
        SectionMap sectionsFromDb)
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
        SectionMap? InitialSections,
        int ExpectedSectionCount,
        int ActualSectionCount,
        bool HasAllExpectedSections,
        bool AllSectionsCataloged);

    private async Task<SectionMap?> RetryFetchUntilCatalogedAsync(
        string publicationCode,
        IFetchProgress? progress,
        CancellationToken cancellationToken)
    {
        logger.Information("MusicInstrumentalSectionListLoader: Starting fetch with retries for publication={PublicationCode}",
            publicationCode);

        progress?.SetIsVisible(true);
        progress?.UpdateProgress(0.0);

        CatalogRetryLoopResult<SectionMap> loopOutcome;
        try
        {
            loopOutcome = await CatalogRetryLoop.RunAsync(
                ctx => RunInstrumentalCatalogRetryIterationAsync(publicationCode, ctx),
                (ex, attempt) => logger.Warning(ex, "MusicInstrumentalSectionListLoader: Attempt {Attempt} failed for publication={PublicationCode}, will retry",
                    attempt, publicationCode),
                cancellationToken);
        }
        finally
        {
            progress?.SetIsVisible(false);
        }

        if (!loopOutcome.AllCataloged)
        {
            logger.Warning("MusicInstrumentalSectionListLoader: Timeout after {Attempts} attempts waiting for all sections to be cataloged for publication {PublicationCode}. Some may still be placeholders.",
                loopOutcome.Attempt, publicationCode);

            return await TryRecoverInstrumentalSectionsAfterTimeoutAsync(publicationCode, loopOutcome.Data);
        }

        return loopOutcome.Data;
    }

    private async Task<SectionMap?> TryRecoverInstrumentalSectionsAfterTimeoutAsync(
        string publicationCode,
        SectionMap? sectionsData)
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

    private async Task<CatalogRetryIterationResult<SectionMap>> RunInstrumentalCatalogRetryIterationAsync(
        string publicationCode,
        CatalogRetryIterationContext context)
    {
        var fetchedWithProgress =
            await mediaService.GetSectionsForPublicationWithoutLanguage(publicationCode);

        await Task.Delay(500, context.CancellationToken);

        var reQueriedData = await mediaService.GetSectionsForPublicationWithoutLanguage(publicationCode);

        var expectedSectionCount = await mediaService.GetExpectedSectionCountForNoLanguagePublicationAsync(publicationCode);
        var actualSectionCount = reQueriedData?.Values.Count ?? 0;

        var hasAllExpectedSections = actualSectionCount >= expectedSectionCount;
        if (AreAllSectionsFullyCataloged(reQueriedData, expectedSectionCount))
        {
            logger.Information("MusicInstrumentalSectionListLoader: All {ExpectedCount} expected sections cataloged on attempt {Attempt} for publication={PublicationCode}",
                expectedSectionCount, context.Attempt, publicationCode);
            return CatalogRetryIterationResult<SectionMap>.Success(reQueriedData!);
        }

        var currentCatalogedCount =
            reQueriedData?.Values.Count(static s =>
                !string.IsNullOrEmpty(s.Name) && s.Id > 0) ?? 0;

        if (currentCatalogedCount > 0 && currentCatalogedCount <= context.PreviousCatalogedCount)
        {
            logger.Information("MusicInstrumentalSectionListLoader: No progress between retries ({CatalogedCount} cataloged, {ExpectedCount} expected). Remaining placeholders are unfetchable. Stopping retries for publication={PublicationCode}",
                currentCatalogedCount, expectedSectionCount, publicationCode);
            return CatalogRetryIterationResult<SectionMap>.Stagnation(reQueriedData);
        }

        LogRetryIterationDiagnostics(context.Attempt, reQueriedData, hasAllExpectedSections, actualSectionCount, expectedSectionCount);

        await Task.Delay(CatalogRetryLoop.DelayMilliseconds(context.Attempt), context.CancellationToken);

        return CatalogRetryIterationResult<SectionMap>.Continue(fetchedWithProgress, currentCatalogedCount);
    }

    private static bool AreAllSectionsFullyCataloged(
        SectionMap? sections,
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
        SectionMap? reQueriedData,
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
