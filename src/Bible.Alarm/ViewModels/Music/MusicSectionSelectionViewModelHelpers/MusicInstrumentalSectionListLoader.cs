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

        // First, check if sections are already harvested (without showing progress)
        var initialSections = await mediaService.GetSectionsForPublicationWithoutLanguage(publicationCode);
        
        // Get expected section count for no-language publications from SectionLanguages discovery table
        var expectedSectionCount = await mediaService.GetExpectedSectionCountForNoLanguagePublicationAsync(publicationCode);
        var actualSectionCount = initialSections?.Values.Count ?? 0;
        
        // Check if we have ALL expected sections AND they're all harvested (not placeholders)
        var hasAllExpectedSections = actualSectionCount >= expectedSectionCount;
        var allSectionsHarvested = hasAllExpectedSections && initialSections != null && initialSections.Count > 0 && initialSections.Values.All(s =>
            !string.IsNullOrEmpty(s.Name) &&
            s.Name != s.SectionCode &&
            s.Id > 0);

        // Do ALL processing on background thread to avoid blocking spinner animation
        var (items, selected) = await Task.Run(async () =>
        {
            SortedDictionary<string, Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublicationSection>? sectionsFromDb = null;
            
            if (!allSectionsHarvested)
            {
                // Sections are not fully harvested - show progress and cancel as soon as fetch is decided, then retry fetching
                progress?.SetIsVisible(true);
                progress?.UpdateProgress(0.0);

                // Retry logic: Retry fetching until all sections are harvested (for future support when sections may not be pre-harvested)
                sectionsFromDb = await RetryFetchUntilHarvestedAsync(publicationCode, progress, cancellationToken);
                progress?.UpdateProgress(0.7);
            }
            else
            {
                // All expected sections are already harvested - use the initial query result, no need to show progress
                logger.Debug("MusicInstrumentalSectionListLoader: All {ExpectedCount} expected sections already harvested for publication={PublicationCode}, skipping fetch",
                    expectedSectionCount, publicationCode);
                sectionsFromDb = initialSections;
            }

            if (sectionsFromDb == null || sectionsFromDb.Count == 0)
            {
                logger.Warning("[MusicSectionSelection] LoadAsync: No sections found for publication={PublicationCode}. This publication may not be harvested yet or may not have sections.",
                    publicationCode);

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
            if (!allSectionsHarvested)
            {
                progress?.UpdateProgress(0.9);
            }

            return (vms, selected);
        });

        // Only update progress if we were fetching (progress was shown)
        if (!allSectionsHarvested)
        {
            progress?.UpdateProgress(1.0);
            progress?.SetIsVisible(false);
        }

        return (items, selected);
    }

    private async Task<SortedDictionary<string, Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublicationSection>?> RetryFetchUntilHarvestedAsync(
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

        logger.Information("MusicInstrumentalSectionListLoader: Starting fetch with retries for publication={PublicationCode}",
            publicationCode);

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
                    // Fetch sections (this may trigger harvesting if support is added in the future)
                    // Pass progress to show download percentage during harvesting
                    // Note: GetSectionsForPublicationWithoutLanguage may call SetIsVisible(true) internally, but we keep it visible between retries
                    sectionsData = await mediaService.GetSectionsForPublicationWithoutLanguage(publicationCode);

                    // Wait a bit for background harvesting to start (with cancellation support)
                    await Task.Delay(500, cancellationToken);

                    // Re-query to check if sections are now harvested (no progress needed for re-query)
                    var reQueriedData = await mediaService.GetSectionsForPublicationWithoutLanguage(publicationCode);

                    // Get expected section count to verify we have all sections
                    var expectedSectionCount = await mediaService.GetExpectedSectionCountForNoLanguagePublicationAsync(publicationCode);
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
                        logger.Information("MusicInstrumentalSectionListLoader: All {ExpectedCount} expected sections harvested on attempt {Attempt} for publication={PublicationCode}",
                            expectedSectionCount, attempt, publicationCode);
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
                                logger.Debug("MusicInstrumentalSectionListLoader: Attempt {Attempt}: Still waiting for {Count} sections to be harvested: {Placeholders}",
                                    attempt, placeholders.Count, string.Join(", ", placeholders));
                            }
                            else if (!hasAllExpectedSections)
                            {
                                logger.Debug("MusicInstrumentalSectionListLoader: Attempt {Attempt}: Only {ActualCount}/{ExpectedCount} sections found, will retry",
                                    attempt, actualSectionCount, expectedSectionCount);
                            }
                        }
                        else
                        {
                            logger.Debug("MusicInstrumentalSectionListLoader: Attempt {Attempt}: No sections found yet, will retry",
                                attempt);
                        }

                        // Re-show overlay after GetSectionsForPublicationWithoutLanguage completes (it may hide it)
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
                    logger.Information("MusicInstrumentalSectionListLoader: Fetch cancelled at attempt {Attempt} for publication={PublicationCode}",
                        attempt, publicationCode);
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

                    // Re-show overlay after exception
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
            logger.Warning("MusicInstrumentalSectionListLoader: Timeout after {Attempts} attempts waiting for all sections to be harvested for publication {PublicationCode}. Some may still be placeholders.",
                attempt, publicationCode);

            // Use the last fetched data even if not all are harvested
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
}

