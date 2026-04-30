#nullable enable

using System.Threading;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;

namespace Bible.Alarm.ViewModels.Music.MusicPublicationSelectionViewModelHelpers;

internal sealed class MusicPublicationFetchCoordinator
{
    private readonly IMediaService mediaService;

    public MusicPublicationFetchCoordinator(IMediaService mediaService)
    {
        this.mediaService = mediaService;
    }

    public async Task<Dictionary<string, BiblePublication>?> FetchMusicPublicationsAsync(
        string? languageCode,
        AlarmMusic? current,
        bool downloadAll,
        IFetchProgress? progress,
        CancellationToken cancellationToken = default)
    {
        // Music type is inferred from LanguageCode: NULL/empty = instrumental, otherwise = vocal
        var isMelodyMusic = string.IsNullOrEmpty(current?.LanguageCode) && string.IsNullOrEmpty(languageCode);
        var effectiveLanguageCode = isMelodyMusic ? AppConstants.Media.DefaultLanguageCode : languageCode;

        if (string.IsNullOrEmpty(effectiveLanguageCode) && !isMelodyMusic)
            return null;

        var languageForFetch = effectiveLanguageCode!;
        Dictionary<string, BiblePublication>? publicationsData = null;

        if (downloadAll)
        {
            // Always check DB first: if all expected publications are already cataloged, use that and skip fetch/progress (matches Bible).
            var initialPublications = await mediaService.GetBiblePublications(languageForFetch, AppConstants.Media.BiblePublicationCategoryMusic, downloadAll: false, null, requireIsMusicForMusicCategory: true);
            var expectedPublicationCount = await mediaService.GetExpectedPublicationCountAsync(languageForFetch, AppConstants.Media.BiblePublicationCategoryMusic, requireIsMusicForMusicCategory: true);
            var actualPublicationCount = initialPublications?.Values.Count ?? 0;
            var hasAllExpected = actualPublicationCount >= expectedPublicationCount;
            var allPublicationsCataloged = hasAllExpected && initialPublications != null && initialPublications.Values.Count > 0 && initialPublications.Values.All(p =>
                !string.IsNullOrEmpty(p.Name) && !string.Equals(p.Name, p.PublicationCode, StringComparison.OrdinalIgnoreCase) && p.Id > 0);

            if (allPublicationsCataloged)
            {
                Serilog.Log.Debug(AppConstants.Logging.PopulateSongPublicationsDiagnosticsLog.AllExpectedAlreadyCatalogedSkippingFetch,
                    expectedPublicationCount, languageForFetch, AppConstants.Media.BiblePublicationCategoryMusic);
                return initialPublications;
            }
        }

        if (isMelodyMusic)
        {
            publicationsData = await mediaService.GetBiblePublications(AppConstants.Media.DefaultLanguageCode, AppConstants.Media.BiblePublicationCategoryMusic, downloadAll, progress, requireIsMusicForMusicCategory: true);
        }
        else if (!string.IsNullOrEmpty(languageCode))
        {
            publicationsData = await mediaService.GetBiblePublications(languageCode, AppConstants.Media.BiblePublicationCategoryMusic, downloadAll, progress, requireIsMusicForMusicCategory: true);
        }
        else
        {
            return null;
        }

        if (downloadAll && !string.IsNullOrEmpty(languageCode) && !languageCode.Equals(AppConstants.Media.DefaultLanguageCode, StringComparison.OrdinalIgnoreCase))
        {
            publicationsData = await RetryFetchUntilCatalogedAsync(languageCode, progress, cancellationToken);
        }

        if (publicationsData == null && !string.IsNullOrEmpty(languageCode))
        {
            publicationsData = await mediaService.GetBiblePublications(languageCode, AppConstants.Media.BiblePublicationCategoryMusic, downloadAll, progress, requireIsMusicForMusicCategory: true);
        }

        return publicationsData;
    }

    private async Task<Dictionary<string, BiblePublication>?> RetryFetchUntilCatalogedAsync(
        string languageCode, 
        IFetchProgress? progress,
        CancellationToken cancellationToken)
    {
        // First, check if publications are already cataloged (without showing progress)
        // This prevents progress bar from flashing at 0% when data is already available
        var initialPublications = await mediaService.GetBiblePublications(languageCode, AppConstants.Media.BiblePublicationCategoryMusic, downloadAll: false, null, requireIsMusicForMusicCategory: true);
        
        // Get expected publication count from PublicationLanguages discovery table
        var expectedPublicationCount = await mediaService.GetExpectedPublicationCountAsync(languageCode, AppConstants.Media.BiblePublicationCategoryMusic, requireIsMusicForMusicCategory: true);
        var actualPublicationCount = initialPublications?.Values.Count ?? 0;
        
        // Check if we have ALL expected publications AND they're all cataloged (not placeholders)
        var hasAllExpectedPublications = actualPublicationCount >= expectedPublicationCount;
        var allPublicationsCataloged = hasAllExpectedPublications && initialPublications != null && initialPublications.Values.Count > 0 && initialPublications.Values.All(p => 
            !string.IsNullOrEmpty(p.Name) && 
            !string.Equals(p.Name, p.PublicationCode, StringComparison.OrdinalIgnoreCase) && 
            p.Id > 0);

        if (allPublicationsCataloged)
        {
            // All expected publications are already cataloged - use the initial query result, no need to show progress
            Serilog.Log.Debug(AppConstants.Logging.PopulateSongPublicationsDiagnosticsLog.AllExpectedAlreadyCatalogedSkippingFetch,
                expectedPublicationCount, languageCode, AppConstants.Media.BiblePublicationCategoryMusic);
            return initialPublications;
        }

        // Publications are not fully cataloged - show progress and retry fetching
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

        Serilog.Log.Information(AppConstants.Logging.PopulateSongPublicationsDiagnosticsLog.StartingFetchWithRetries,
            languageCode, AppConstants.Media.BiblePublicationCategoryMusic);

        // Show progress overlay at the start of retry loop and keep it visible throughout all retries
        progress?.SetIsVisible(true);
        progress?.UpdateProgress(0.0);

        Dictionary<string, BiblePublication>? publicationsData = null;

        try
        {
            while (!allCataloged && attempt < maxRetries && (DateTime.UtcNow - startTime) < maxWaitTime)
            {
                // Check for cancellation before each attempt
                cancellationToken.ThrowIfCancellationRequested();
                
                attempt++;

                try
                {
                    // Fetch publications (this triggers cataloging if needed)
                    // Pass progress to show download percentage during cataloging
                    publicationsData = await mediaService.GetBiblePublications(languageCode, AppConstants.Media.BiblePublicationCategoryMusic, downloadAll: true, progress, requireIsMusicForMusicCategory: true);

                    // Wait a bit for background cataloging to start (with cancellation support)
                    await Task.Delay(500, cancellationToken);

                    // Re-query to check if publications are now cataloged (no progress needed for re-query)
                    var reQueriedData = await mediaService.GetBiblePublications(languageCode, AppConstants.Media.BiblePublicationCategoryMusic, downloadAll: false, null, requireIsMusicForMusicCategory: true);

                    // Get expected publication count to verify we have all publications
                    var retryExpectedCount = await mediaService.GetExpectedPublicationCountAsync(languageCode, AppConstants.Media.BiblePublicationCategoryMusic, requireIsMusicForMusicCategory: true);
                    var retryActualCount = reQueriedData?.Values.Count ?? 0;
                    
                    // Check if we have ALL expected publications AND they're all cataloged (not placeholders)
                    // A publication is cataloged if it has a name that's different from its code and has an ID > 0
                    var retryHasAllExpected = retryActualCount >= retryExpectedCount;
                    var retryAllCataloged = retryHasAllExpected && reQueriedData != null && reQueriedData.Values.Count > 0 && reQueriedData.Values.All(p =>
                        !string.IsNullOrEmpty(p.Name) &&
                        !string.Equals(p.Name, p.PublicationCode, StringComparison.OrdinalIgnoreCase) &&
                        p.Id > 0);

                    if (retryAllCataloged)
                    {
                        publicationsData = reQueriedData;
                        allCataloged = true;
                        Serilog.Log.Information(AppConstants.Logging.PopulateSongPublicationsDiagnosticsLog.AllExpectedPublicationsCatalogedOnAttempt,
                            retryExpectedCount, attempt, languageCode);
                    }
                    else
                    {
                        var currentCatalogedCount = reQueriedData?.Values.Count(p =>
                            !string.IsNullOrEmpty(p.Name) && !string.Equals(p.Name, p.PublicationCode, StringComparison.OrdinalIgnoreCase) && p.Id > 0) ?? 0;

                        if (currentCatalogedCount > 0 && currentCatalogedCount <= previousCatalogedCount)
                        {
                            Serilog.Log.Information(AppConstants.Logging.PopulateSongPublicationsDiagnosticsLog.NoProgressBetweenRetriesStopping,
                                currentCatalogedCount, retryExpectedCount, languageCode);
                            publicationsData = reQueriedData;
                            break;
                        }
                        previousCatalogedCount = currentCatalogedCount;

                        // Log which publications are still placeholders or missing for debugging
                        if (reQueriedData != null)
                        {
                            var placeholders = reQueriedData.Values.Where(p =>
                                string.IsNullOrEmpty(p.Name) ||
                                string.Equals(p.Name, p.PublicationCode, StringComparison.OrdinalIgnoreCase) ||
                                p.Id == 0).Select(p => p.PublicationCode).ToList();

                            if (placeholders.Count > 0)
                            {
                                Serilog.Log.Debug(AppConstants.Logging.PopulateSongPublicationsDiagnosticsLog.AttemptStillWaitingForPlaceholders,
                                    attempt, placeholders.Count, string.Join(", ", placeholders));
                            }
                            else if (!retryHasAllExpected)
                            {
                                Serilog.Log.Debug(AppConstants.Logging.PopulateSongPublicationsDiagnosticsLog.AttemptPartialPublicationsRetry,
                                    attempt, retryActualCount, retryExpectedCount);
                            }
                        }
                        else
                        {
                            Serilog.Log.Debug(AppConstants.Logging.PopulateSongPublicationsDiagnosticsLog.AttemptNoPublicationsYetRetry,
                                attempt);
                        }

                        // Wait with increasing delay before retrying (1s, 2s, 3s, etc., up to 5s) - with cancellation support
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

                    Serilog.Log.Warning(ex, AppConstants.Logging.PopulateSongPublicationsDiagnosticsLog.AttemptFailedWillRetry,
                        attempt, languageCode);

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
            Serilog.Log.Warning(AppConstants.Logging.PopulateSongPublicationsDiagnosticsLog.TimeoutAfterAttemptsWaitingForCatalog,
                attempt, languageCode);

            // Use the last fetched data even if not all are cataloged
            if (publicationsData == null || publicationsData.Count == 0)
            {
                // Final attempt to get at least some data
                try
                {
                    publicationsData = await mediaService.GetBiblePublications(languageCode, AppConstants.Media.BiblePublicationCategoryMusic, downloadAll: false, progress, requireIsMusicForMusicCategory: true);
                }
                catch (Exception ex)
                {
                    Serilog.Log.Error(ex, AppConstants.Logging.PopulateSongPublicationsDiagnosticsLog.FinalFetchAttemptFailed,
                        languageCode);
                }
            }
        }

        return publicationsData;
    }
}
