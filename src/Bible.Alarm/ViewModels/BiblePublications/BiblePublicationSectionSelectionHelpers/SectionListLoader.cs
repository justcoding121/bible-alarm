#nullable enable

using System;
using System.Collections.Generic;
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
        // Get cancellation token from progress tracker (same CTS from modal)
        var cancellationToken = progress?.CancellationToken ?? CancellationToken.None;
        
        SortedDictionary<string, Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublicationSection>? sectionsFromDb = null;
        
        // Retry logic: If non-English language, retry fetching until all sections are cataloged
        // For non-English languages, sections need to be fetched, so we retry with increasing delays
        if (!string.IsNullOrEmpty(languageCode) && !languageCode.Equals(AppConstants.Media.DefaultLanguageCode, StringComparison.OrdinalIgnoreCase))
        {
            var initialSections = await mediaService.GetBiblePublicationSections(languageCode, publicationCode, null);

            var expectedSectionCount = await mediaService.GetExpectedSectionCountAsync(languageCode, publicationCode);
            var actualSectionCount = initialSections?.Values.Count ?? 0;
            var hasAllExpectedSections = actualSectionCount >= expectedSectionCount;

            if (AreAllSectionsFullyCataloged(initialSections, expectedSectionCount))
            {
                logger.Debug("SectionListLoader: All {ExpectedCount} expected sections already cataloged for publication={PublicationCode}, language={LanguageCode}, skipping fetch",
                    expectedSectionCount, publicationCode, languageCode);
                sectionsFromDb = initialSections;
            }
            else
            {
                progress?.SetIsVisible(true);
                progress?.UpdateProgress(0.0);

                if (hasAllExpectedSections)
                {
                    logger.Debug("SectionListLoader: Have {ActualCount} sections but some are placeholders, fetching remaining sections for publication={PublicationCode}, language={LanguageCode}",
                        actualSectionCount, publicationCode, languageCode);
                }
                else
                {
                    logger.Debug("SectionListLoader: Only {ActualCount}/{ExpectedCount} sections found, fetching remaining sections for publication={PublicationCode}, language={LanguageCode}",
                        actualSectionCount, expectedSectionCount, publicationCode, languageCode);
                }

                sectionsFromDb = await RetryFetchUntilCatalogedAsync(languageCode, publicationCode, progress, cancellationToken);
            }
        }
        else
        {
            // For English or when language is empty, just fetch once
            sectionsFromDb = await mediaService.GetBiblePublicationSections(languageCode, publicationCode, progress);
        }

        progress?.UpdateProgress(0.7);

        if (sectionsFromDb == null || sectionsFromDb.Count == 0)
        {
            logger.Warning(
                "SectionListLoader: No sections found for publication={PublicationCode}, language={LanguageCode}. This publication may not be cataloged yet or may not have sections.",
                publicationCode,
                languageCode ?? "(null)");

            progress?.UpdateProgress(1.0);
            progress?.SetIsVisible(false);
            return (new List<BiblePublicationSectionListViewItemModel>(), new Dictionary<string, BiblePublicationSectionListViewItemModel>(StringComparer.OrdinalIgnoreCase));
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
            logger.Information("SectionListLoader: Removed {Count} unfetchable placeholder sections: {Codes}",
                unfetchableSectionCodes.Count, string.Join(", ", unfetchableSectionCodes));
        }

        if (sectionsFromDb.Count == 0)
        {
            progress?.UpdateProgress(1.0);
            progress?.SetIsVisible(false);
            return (new List<BiblePublicationSectionListViewItemModel>(), new Dictionary<string, BiblePublicationSectionListViewItemModel>(StringComparer.OrdinalIgnoreCase));
        }

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

        // Sort using natural sort (numeric sections as int, non-numeric as string)
        vms.Sort();

        progress?.UpdateProgress(0.9);
        progress?.UpdateProgress(1.0);
        progress?.SetIsVisible(false);

        return (vms, map);
    }

    private async Task<SortedDictionary<string, Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublicationSection>?> RetryFetchUntilCatalogedAsync(
        string languageCode,
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

        logger.Information("SectionListLoader: Starting fetch with retries for publication={PublicationCode}, language={LanguageCode}",
            publicationCode, languageCode);

        // Show progress overlay at the start of retry loop and keep it visible throughout all retries
        progress?.SetIsVisible(true);
        progress?.UpdateProgress(0.0);

        await NetworkExceptionHelper.ThrowIfNoInternetAsync(internetChecker);

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
                    // Fetch sections (this triggers cataloging if needed)
                    // Pass progress to show download percentage during cataloging
                    sectionsData = await mediaService.GetBiblePublicationSections(languageCode, publicationCode, progress);

                    // Wait a bit for background cataloging to start (with cancellation support)
                    await Task.Delay(500, cancellationToken);

                    // Re-query to check if sections are now cataloged (no progress needed for re-query)
                    var reQueriedData = await mediaService.GetBiblePublicationSections(languageCode, publicationCode, null);

                    var expectedSectionCount = await mediaService.GetExpectedSectionCountAsync(languageCode, publicationCode);
                    var actualSectionCount = reQueriedData?.Values.Count ?? 0;

                    var hasAllExpectedSections = actualSectionCount >= expectedSectionCount;
                    if (AreAllSectionsFullyCataloged(reQueriedData, expectedSectionCount))
                    {
                        sectionsData = reQueriedData;
                        allCataloged = true;
                        logger.Information("SectionListLoader: All {ExpectedCount} expected sections cataloged on attempt {Attempt} for publication={PublicationCode}, language={LanguageCode}",
                            expectedSectionCount, attempt, publicationCode, languageCode);
                    }
                    else
                    {
                        var currentCatalogedCount = reQueriedData?.Values.Count(s =>
                            !string.IsNullOrEmpty(s.Name) && s.Id > 0) ?? 0;

                        if (currentCatalogedCount > 0 && currentCatalogedCount <= previousCatalogedCount)
                        {
                            logger.Information("SectionListLoader: No progress between retries ({CatalogedCount} cataloged, {ExpectedCount} expected). Remaining placeholders are unfetchable. Stopping retries for publication={PublicationCode}, language={LanguageCode}",
                                currentCatalogedCount, expectedSectionCount, publicationCode, languageCode);
                            sectionsData = reQueriedData;
                            break;
                        }
                        previousCatalogedCount = currentCatalogedCount;

                        LogSectionRetryIterationDiagnostics(attempt, reQueriedData, hasAllExpectedSections, actualSectionCount, expectedSectionCount);

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

                    logger.Warning(ex, "SectionListLoader: Attempt {Attempt} failed for publication={PublicationCode}, language={LanguageCode}, will retry",
                        attempt, publicationCode, languageCode);

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
            logger.Warning("SectionListLoader: Timeout after {Attempts} attempts waiting for all sections to be cataloged for publication {PublicationCode}, language {LanguageCode}. Some may still be placeholders.",
                attempt, publicationCode, languageCode);

            // Use the last fetched data even if not all are cataloged
            if (sectionsData == null || sectionsData.Count == 0)
            {
                // Final attempt to get at least some data
                try
                {
                    sectionsData = await mediaService.GetBiblePublicationSections(languageCode, publicationCode, null);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "SectionListLoader: Final fetch attempt failed for publication={PublicationCode}, language={LanguageCode}",
                        publicationCode, languageCode);
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

