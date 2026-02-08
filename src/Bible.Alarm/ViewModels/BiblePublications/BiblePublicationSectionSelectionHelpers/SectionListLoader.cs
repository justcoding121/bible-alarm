#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Serilog;

namespace Bible.Alarm.ViewModels.BiblePublications.BiblePublicationSectionSelectionHelpers;

internal sealed class SectionListLoader
{
    private readonly ILogger logger;
    private readonly IMediaService mediaService;

    public SectionListLoader(ILogger logger, IMediaService mediaService)
    {
        this.logger = logger;
        this.mediaService = mediaService;
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
        
        // Retry logic: If non-English language, retry fetching until all sections are harvested
        // For non-English languages, sections need to be fetched, so we retry with increasing delays
        if (!string.IsNullOrEmpty(languageCode) && !languageCode.Equals(AppConstants.Media.DefaultLanguageCode, StringComparison.OrdinalIgnoreCase))
        {
            // First, check if sections are already harvested (without showing progress)
            // This prevents progress bar from flashing at 0% when data is already available
            var initialSections = await mediaService.GetBiblePublicationSections(languageCode, publicationCode, null);
            
            // Get expected section count from SectionLanguages discovery table
            var expectedSectionCount = await mediaService.GetExpectedSectionCountAsync(languageCode, publicationCode);
            var actualSectionCount = initialSections?.Values.Count ?? 0;
            
            // Check if we have ALL expected sections AND they're all harvested (not placeholders)
            var hasAllExpectedSections = actualSectionCount >= expectedSectionCount;
            var allSectionsHarvested = hasAllExpectedSections && initialSections != null && initialSections.Values.Count > 0 && initialSections.Values.All(s =>
                !string.IsNullOrEmpty(s.Name) &&
                s.Name != s.SectionCode &&
                s.Id > 0);

            if (allSectionsHarvested)
            {
                // All expected sections are already harvested - use the initial query result, no need to show progress
                logger.Debug("SectionListLoader: All {ExpectedCount} expected sections already harvested for publication={PublicationCode}, language={LanguageCode}, skipping fetch",
                    expectedSectionCount, publicationCode, languageCode);
                sectionsFromDb = initialSections;
            }
            else
            {
                // Not all sections are harvested - show progress and retry fetching
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
                sectionsFromDb = await RetryFetchUntilHarvestedAsync(languageCode, publicationCode, progress, cancellationToken);
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
                "SectionListLoader: No sections found for publication={PublicationCode}, language={LanguageCode}. This publication may not be harvested yet or may not have sections.",
                publicationCode,
                languageCode ?? "(null)");

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

    private async Task<SortedDictionary<string, Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublicationSection>?> RetryFetchUntilHarvestedAsync(
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
        var allHarvested = false;
        var attempt = 0;

        logger.Information("SectionListLoader: Starting fetch with retries for publication={PublicationCode}, language={LanguageCode}",
            publicationCode, languageCode);

        // Show progress overlay at the start of retry loop and keep it visible throughout all retries
        progress?.SetIsVisible(true);
        progress?.UpdateProgress(0.0);

        SortedDictionary<string, Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublicationSection>? sectionsData = null;

        try
        {
            while (!allHarvested && attempt < maxRetries && (DateTime.UtcNow - startTime) < maxWaitTime)
            {
                // Check for cancellation before each attempt
                cancellationToken.ThrowIfCancellationRequested();

                attempt++;

                try
                {
                    // Fetch sections (this triggers harvesting if needed)
                    // Pass progress to show download percentage during harvesting
                    // Note: GetBiblePublicationSections will call SetIsVisible(true) internally, but we keep it visible between retries
                    sectionsData = await mediaService.GetBiblePublicationSections(languageCode, publicationCode, progress);

                    // Wait a bit for background harvesting to start (with cancellation support)
                    await Task.Delay(500, cancellationToken);

                    // Re-query to check if sections are now harvested (no progress needed for re-query)
                    var reQueriedData = await mediaService.GetBiblePublicationSections(languageCode, publicationCode, null);

                    // Get expected section count to verify we have all sections
                    var expectedSectionCount = await mediaService.GetExpectedSectionCountAsync(languageCode, publicationCode);
                    var actualSectionCount = reQueriedData?.Values.Count ?? 0;
                    
                    // Check if we have ALL expected sections AND they're all harvested (not placeholders)
                    // A section is harvested if it has a name that's different from its code and has an ID > 0
                    var hasAllExpectedSections = actualSectionCount >= expectedSectionCount;
                    var allSectionsHarvested = hasAllExpectedSections && reQueriedData != null && reQueriedData.Values.Count > 0 && reQueriedData.Values.All(s =>
                        !string.IsNullOrEmpty(s.Name) &&
                        s.Name != s.SectionCode &&
                        s.Id > 0);

                    if (allSectionsHarvested)
                    {
                        sectionsData = reQueriedData;
                        allHarvested = true;
                        logger.Information("SectionListLoader: All {ExpectedCount} expected sections harvested on attempt {Attempt} for publication={PublicationCode}, language={LanguageCode}",
                            expectedSectionCount, attempt, publicationCode, languageCode);
                    }
                    else
                    {
                        // Log which sections are still placeholders or missing for debugging
                        if (reQueriedData != null)
                        {
                            var placeholders = reQueriedData.Values.Where(s =>
                                string.IsNullOrEmpty(s.Name) ||
                                s.Name == s.SectionCode ||
                                s.Id == 0).Select(s => s.SectionCode).ToList();

                            if (placeholders.Count > 0)
                            {
                                logger.Debug("SectionListLoader: Attempt {Attempt}: Still waiting for {Count} sections to be harvested: {Placeholders}",
                                    attempt, placeholders.Count, string.Join(", ", placeholders));
                            }
                            else if (!hasAllExpectedSections)
                            {
                                logger.Debug("SectionListLoader: Attempt {Attempt}: Only {ActualCount}/{ExpectedCount} sections found, will retry",
                                    attempt, actualSectionCount, expectedSectionCount);
                            }
                            else if (actualSectionCount == 0)
                            {
                                logger.Debug("SectionListLoader: Attempt {Attempt}: No sections found yet, will retry",
                                    attempt);
                            }
                        }

                        // Re-show overlay after GetBiblePublicationSections completes (it hides it in finally block)
                        // This keeps the overlay visible during retry delays
                        if (!allHarvested && attempt < maxRetries && (DateTime.UtcNow - startTime) < maxWaitTime)
                        {
                            progress?.SetIsVisible(true);
                        }

                        // Wait with increasing delay before retrying (1s, 2s, 3s, etc., up to 5s) - with cancellation support
                        var delay = Math.Min(retryDelay * attempt, 5000);
                        await Task.Delay(delay, cancellationToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Re-throw cancellation - data saved so far is preserved
                    logger.Information("SectionListLoader: Fetch cancelled at attempt {Attempt} for publication={PublicationCode}, language={LanguageCode}",
                        attempt, publicationCode, languageCode);
                    throw;
                }
                catch (Exception ex)
                {
                    logger.Warning(ex, "SectionListLoader: Attempt {Attempt} failed for publication={PublicationCode}, language={LanguageCode}, will retry",
                        attempt, publicationCode, languageCode);

                    // Re-show overlay after exception (GetBiblePublicationSections hides it in finally block)
                    // This keeps the overlay visible during retry delays
                    if (attempt < maxRetries && (DateTime.UtcNow - startTime) < maxWaitTime)
                    {
                        progress?.SetIsVisible(true);
                    }

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

        if (!allHarvested)
        {
            logger.Warning("SectionListLoader: Timeout after {Attempts} attempts waiting for all sections to be harvested for publication {PublicationCode}, language {LanguageCode}. Some may still be placeholders.",
                attempt, publicationCode, languageCode);

            // Use the last fetched data even if not all are harvested
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
}

