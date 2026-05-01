#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
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
        // Get cancellation token from progress tracker (same CTS from modal)
        var cancellationToken = progress?.CancellationToken ?? CancellationToken.None;

        // First, check if sections are already cataloged (without showing progress)
        var initialSections = await mediaService.GetSectionsForPublicationWithoutLanguage(publicationCode);
        
        var expectedSectionCount = await mediaService.GetExpectedSectionCountForNoLanguagePublicationAsync(publicationCode);
        var actualSectionCount = initialSections?.Values.Count ?? 0;
        var hasAllExpectedSections = actualSectionCount >= expectedSectionCount;
        var allSectionsCataloged = AreAllSectionsFullyCataloged(initialSections, expectedSectionCount);

        // Do ALL processing on background thread to avoid blocking spinner animation
        var (items, selected) = await Task.Run(async () =>
        {
            SortedDictionary<string, Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublicationSection>? sectionsFromDb = null;

            if (!allSectionsCataloged)
            {
                progress?.SetIsVisible(true);
                progress?.UpdateProgress(0.0);

                if (hasAllExpectedSections)
                {
                    logger.Debug("MusicInstrumentalSectionListLoader: Have {ActualCount} sections but some are placeholders, fetching remaining for publication={PublicationCode}",
                        actualSectionCount, publicationCode);
                }
                else
                {
                    logger.Debug("MusicInstrumentalSectionListLoader: Only {ActualCount}/{ExpectedCount} sections found, fetching remaining for publication={PublicationCode}",
                        actualSectionCount, expectedSectionCount, publicationCode);
                }

                sectionsFromDb = await RetryFetchUntilCatalogedAsync(publicationCode, progress, cancellationToken);
                progress?.UpdateProgress(0.7);
            }
            else
            {
                logger.Debug("MusicInstrumentalSectionListLoader: All {ExpectedCount} expected sections already cataloged for publication={PublicationCode}, skipping fetch",
                    expectedSectionCount, publicationCode);
                sectionsFromDb = initialSections;
            }

            if (sectionsFromDb == null || sectionsFromDb.Count == 0)
            {
                logger.Warning("[MusicSectionSelection] LoadAsync: No sections found for publication={PublicationCode}. This publication may not be cataloged yet or may not have sections.",
                    publicationCode);

                return (new List<BiblePublicationSectionListViewItemModel>(), (BiblePublicationSectionListViewItemModel?)null);
            }

            // Remove placeholder sections that couldn't be fetched (e.g. no content on the server)
            var unfetchableSectionCodes = sectionsFromDb.Values
                .Where(s => s.Id == 0 || string.IsNullOrEmpty(s.Name))
                .Select(s => s.SectionCode)
                .ToList();
            if (unfetchableSectionCodes.Count > 0)
            {
                foreach (var code in unfetchableSectionCodes)
                {
                    sectionsFromDb.Remove(code);
                }
                logger.Information("MusicInstrumentalSectionListLoader: Removed {Count} unfetchable placeholder sections: {Codes}",
                    unfetchableSectionCodes.Count, string.Join(", ", unfetchableSectionCodes));
            }

            if (sectionsFromDb.Count == 0)
            {
                return (new List<BiblePublicationSectionListViewItemModel>(), (BiblePublicationSectionListViewItemModel?)null);
            }

            var vms = new List<BiblePublicationSectionListViewItemModel>();
            BiblePublicationSectionListViewItemModel? selected = null;

            foreach (var section in sectionsFromDb.Values)
            {
                var sectionVm = new BiblePublicationSectionListViewItemModel(section);
                vms.Add(sectionVm);

                if (!string.IsNullOrEmpty(selectedSectionCode) &&
                    section.SectionCode.Equals(selectedSectionCode, StringComparison.OrdinalIgnoreCase))
                {
                    selected = sectionVm;
                    selected.IsSelected = true;
                }
            }

            // Sort using natural sort (numeric sections as int, non-numeric as string)
            vms.Sort();

            // Only update progress if we were fetching (progress was shown)
            if (!allSectionsCataloged)
            {
                progress?.UpdateProgress(0.9);
            }

            return (vms, selected);
        });

        // Only update progress if we were fetching (progress was shown)
        if (!allSectionsCataloged)
        {
            progress?.UpdateProgress(1.0);
            progress?.SetIsVisible(false);
        }

        return (items, selected);
    }

    private async Task<SortedDictionary<string, Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublicationSection>?> RetryFetchUntilCatalogedAsync(
        string publicationCode,
        IFetchProgress? progress,
        CancellationToken cancellationToken)
    {
        // Up to 10 retries
        const int maxRetries = 10;
        // Start with 1 second
        var retryDelay = 1000;
        // Total max wait time of 60 seconds
        var maxWaitTime = TimeSpan.FromSeconds(60);
        var startTime = DateTime.UtcNow;
        var allCataloged = false;
        var attempt = 0;
        var previousCatalogedCount = -1;

        logger.Information("MusicInstrumentalSectionListLoader: Starting fetch with retries for publication={PublicationCode}",
            publicationCode);

        // Show progress overlay at the start of retry loop and keep it visible throughout all retries
        progress?.SetIsVisible(true);
        progress?.UpdateProgress(0.0);

        SortedDictionary<string, Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublicationSection>? sectionsData = null;

        try
        {
            while (!allCataloged && attempt < maxRetries && (DateTime.UtcNow - startTime) < maxWaitTime)
            {
                // Check for cancellation before each attempt
                cancellationToken.ThrowIfCancellationRequested();

                attempt++;

                try
                {
                    // Fetch sections (this may trigger cataloging if support is added in the future)
                    // Pass progress to show download percentage during cataloging
                    sectionsData = await mediaService.GetSectionsForPublicationWithoutLanguage(publicationCode);

                    // Wait a bit for background cataloging to start (with cancellation support)
                    await Task.Delay(500, cancellationToken);

                    // Re-query to check if sections are now cataloged (no progress needed for re-query)
                    var reQueriedData = await mediaService.GetSectionsForPublicationWithoutLanguage(publicationCode);

                    var expectedSectionCount = await mediaService.GetExpectedSectionCountForNoLanguagePublicationAsync(publicationCode);
                    var actualSectionCount = reQueriedData?.Values.Count ?? 0;

                    var hasAllExpectedSections = actualSectionCount >= expectedSectionCount;
                    if (AreAllSectionsFullyCataloged(reQueriedData, expectedSectionCount))
                    {
                        sectionsData = reQueriedData;
                        allCataloged = true;
                        logger.Information("MusicInstrumentalSectionListLoader: All {ExpectedCount} expected sections cataloged on attempt {Attempt} for publication={PublicationCode}",
                            expectedSectionCount, attempt, publicationCode);
                    }
                    else
                    {
                        var currentCatalogedCount = reQueriedData?.Values.Count(s =>
                            !string.IsNullOrEmpty(s.Name) && s.Id > 0) ?? 0;

                        if (currentCatalogedCount > 0 && currentCatalogedCount <= previousCatalogedCount)
                        {
                            logger.Information("MusicInstrumentalSectionListLoader: No progress between retries ({CatalogedCount} cataloged, {ExpectedCount} expected). Remaining placeholders are unfetchable. Stopping retries for publication={PublicationCode}",
                                currentCatalogedCount, expectedSectionCount, publicationCode);
                            sectionsData = reQueriedData;
                            break;
                        }
                        previousCatalogedCount = currentCatalogedCount;

                        LogRetryIterationDiagnostics(attempt, reQueriedData, hasAllExpectedSections, actualSectionCount, expectedSectionCount);

                        var delay = Math.Min(retryDelay * attempt, 5000);
                        await Task.Delay(delay, cancellationToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Re-throw cancellation - data saved so far is preserved
                    throw;
                }
                catch (System.Net.Http.HttpRequestException)
                {
                    throw;
                }
                catch (System.Net.Sockets.SocketException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    if (NetworkExceptionHelper.IsNetworkFailure(ex))
                    {
                        throw;
                    }

                    logger.Warning(ex, "MusicInstrumentalSectionListLoader: Attempt {Attempt} failed for publication={PublicationCode}, will retry",
                        attempt, publicationCode);

                    // Wait before retrying on exception (with cancellation support)
                    var delay = Math.Min(retryDelay * attempt, 5000);
                    await Task.Delay(delay, cancellationToken);
                }
            }
        }
        finally
        {
            // Hide progress overlay when retry loop completes (success, timeout, or cancellation)
            progress?.SetIsVisible(false);
        }

        if (!allCataloged)
        {
            logger.Warning("MusicInstrumentalSectionListLoader: Timeout after {Attempts} attempts waiting for all sections to be cataloged for publication {PublicationCode}. Some may still be placeholders.",
                attempt, publicationCode);

            // Use the last fetched data even if not all are cataloged
            if (sectionsData == null || sectionsData.Count == 0)
            {
                // Final attempt to get at least some data
                try
                {
                    sectionsData = await mediaService.GetSectionsForPublicationWithoutLanguage(publicationCode);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "MusicInstrumentalSectionListLoader: Final fetch attempt failed for publication={PublicationCode}",
                        publicationCode);
                }
            }
        }

        return sectionsData;
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

        return sections.Values.All(s =>
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
            var placeholders = reQueriedData.Values.Where(s =>
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

