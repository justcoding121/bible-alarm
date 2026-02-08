#nullable enable

using System.Threading;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
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
        // For instrumental music, we need publications without language
        // For vocal music, we need publications with language (or both)
        Dictionary<string, BiblePublication>? publicationsData = null;
        
        // Check if this is instrumental music (no language code)
        var isMelodyMusic = string.IsNullOrEmpty(current?.LanguageCode) && string.IsNullOrEmpty(languageCode);

        if (isMelodyMusic)
        {
            // When in melody mode, show BOTH melody (e.g. Kingdom Melodies) and vocal English (e.g. Original Songs)
            // so the user can switch without having to select "Vocal" and language first.
            // GetBiblePublications with default language code returns publications with default language and publications without language FK.
            publicationsData = await mediaService.GetBiblePublications(AppConstants.Media.DefaultLanguageCode, "Music", downloadAll, progress);
        }
        else if (!string.IsNullOrEmpty(languageCode))
        {
            // Language selected - GetBiblePublications returns BOTH:
            // 1. Publications with LanguageId != null (filtered by language code)
            // 2. Publications with LanguageId == null (no language FK)
            // This is data-driven and works for any category
            publicationsData = await mediaService.GetBiblePublications(languageCode, "Music", downloadAll, progress);
        }
        else
        {
            return null;
        }

        // Retry logic: If downloadAll=true and non-English, retry fetching until all publications are harvested
        // For non-English languages, publications need to be fetched, so we retry with increasing delays
        if (downloadAll && !string.IsNullOrEmpty(languageCode) && !languageCode.Equals(AppConstants.Media.DefaultLanguageCode, StringComparison.OrdinalIgnoreCase))
        {
            publicationsData = await RetryFetchUntilHarvestedAsync(languageCode, progress, cancellationToken);
        }

        // Final fallback if nothing came back
        if (publicationsData == null && !string.IsNullOrEmpty(languageCode))
        {
            publicationsData = await mediaService.GetBiblePublications(languageCode, "Music", downloadAll, progress);
        }

        return publicationsData;
    }

    private async Task<Dictionary<string, BiblePublication>?> RetryFetchUntilHarvestedAsync(
        string languageCode, 
        IFetchProgress? progress,
        CancellationToken cancellationToken)
    {
        // First, check if publications are already harvested (without showing progress)
        // This prevents progress bar from flashing at 0% when data is already available
        var initialPublications = await mediaService.GetBiblePublications(languageCode, "Music", downloadAll: false, null);
        
        // Get expected publication count from PublicationLanguages discovery table
        var expectedPublicationCount = await mediaService.GetExpectedPublicationCountAsync(languageCode, "Music");
        var actualPublicationCount = initialPublications?.Values.Count ?? 0;
        
        // Check if we have ALL expected publications AND they're all harvested (not placeholders)
        var hasAllExpectedPublications = actualPublicationCount >= expectedPublicationCount;
        var allPublicationsHarvested = hasAllExpectedPublications && initialPublications != null && initialPublications.Values.Count > 0 && initialPublications.Values.All(p => 
            !string.IsNullOrEmpty(p.Name) && 
            p.Name != p.PublicationCode && 
            p.Id > 0);

        if (allPublicationsHarvested)
        {
            // All expected publications are already harvested - use the initial query result, no need to show progress
            Serilog.Log.Debug("PopulateSongPublications: All {ExpectedCount} expected publications already harvested for language={LanguageCode}, category=Music, skipping fetch",
                expectedPublicationCount, languageCode);
            return initialPublications;
        }

        // Publications are not fully harvested - show progress and retry fetching
        // Up to 10 retries
        const int maxRetries = 10;
        // Start with 1 second
        var retryDelay = 1000;
        // Total max wait time of 60 seconds
        var maxWaitTime = TimeSpan.FromSeconds(60);
        var startTime = DateTime.UtcNow;
        var allHarvested = false;
        var attempt = 0;

        Serilog.Log.Information("PopulateSongPublications: Starting fetch with retries for language={LanguageCode}, category=Music",
            languageCode);

        // Show progress overlay at the start of retry loop and keep it visible throughout all retries
        progress?.SetIsVisible(true);
        progress?.UpdateProgress(0.0);

        Dictionary<string, BiblePublication>? publicationsData = null;

        try
        {
            while (!allHarvested && attempt < maxRetries && (DateTime.UtcNow - startTime) < maxWaitTime)
            {
                // Check for cancellation before each attempt
                cancellationToken.ThrowIfCancellationRequested();
                
                attempt++;

                try
                {
                    // Fetch publications (this triggers harvesting if needed)
                    // Pass progress to show download percentage during harvesting
                    // Note: GetBiblePublications will call SetIsVisible(true) internally, but we keep it visible between retries
                    publicationsData = await mediaService.GetBiblePublications(languageCode, "Music", downloadAll: true, progress);

                    // Wait a bit for background harvesting to start (with cancellation support)
                    await Task.Delay(500, cancellationToken);

                    // Re-query to check if publications are now harvested (no progress needed for re-query)
                    var reQueriedData = await mediaService.GetBiblePublications(languageCode, "Music", downloadAll: false, null);

                    // Get expected publication count to verify we have all publications
                    var retryExpectedCount = await mediaService.GetExpectedPublicationCountAsync(languageCode, "Music");
                    var retryActualCount = reQueriedData?.Values.Count ?? 0;
                    
                    // Check if we have ALL expected publications AND they're all harvested (not placeholders)
                    // A publication is harvested if it has a name that's different from its code and has an ID > 0
                    var retryHasAllExpected = retryActualCount >= retryExpectedCount;
                    var retryAllHarvested = retryHasAllExpected && reQueriedData != null && reQueriedData.Values.Count > 0 && reQueriedData.Values.All(p =>
                        !string.IsNullOrEmpty(p.Name) &&
                        p.Name != p.PublicationCode &&
                        p.Id > 0);

                    if (retryAllHarvested)
                    {
                        publicationsData = reQueriedData;
                        allHarvested = true;
                        Serilog.Log.Information("PopulateSongPublications: All {ExpectedCount} expected publications harvested on attempt {Attempt} for language={LanguageCode}",
                            retryExpectedCount, attempt, languageCode);
                    }
                    else
                    {
                        // Log which publications are still placeholders or missing for debugging
                        if (reQueriedData != null)
                        {
                            var placeholders = reQueriedData.Values.Where(p =>
                                string.IsNullOrEmpty(p.Name) ||
                                p.Name == p.PublicationCode ||
                                p.Id == 0).Select(p => p.PublicationCode).ToList();

                            if (placeholders.Count > 0)
                            {
                                Serilog.Log.Debug("PopulateSongPublications: Attempt {Attempt}: Still waiting for {Count} publications to be harvested: {Placeholders}",
                                    attempt, placeholders.Count, string.Join(", ", placeholders));
                            }
                            else if (!retryHasAllExpected)
                            {
                                Serilog.Log.Debug("PopulateSongPublications: Attempt {Attempt}: Only {ActualCount}/{ExpectedCount} publications found, will retry",
                                    attempt, retryActualCount, retryExpectedCount);
                            }
                        }
                        else
                        {
                            Serilog.Log.Debug("PopulateSongPublications: Attempt {Attempt}: No publications found yet, will retry",
                                attempt);
                        }

                        // Re-show overlay after GetBiblePublications completes (it hides it in finally block)
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
                    Serilog.Log.Information("PopulateSongPublications: Fetch cancelled at attempt {Attempt} for language={LanguageCode}",
                        attempt, languageCode);
                    throw;
                }
                catch (Exception ex)
                {
                    Serilog.Log.Warning(ex, "PopulateSongPublications: Attempt {Attempt} failed for language={LanguageCode}, will retry",
                        attempt, languageCode);

                    // Re-show overlay after exception (GetBiblePublications hides it in finally block)
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
            Serilog.Log.Warning("PopulateSongPublications: Timeout after {Attempts} attempts waiting for all publications to be harvested for language {LanguageCode}. Some may still be placeholders.",
                attempt, languageCode);

            // Use the last fetched data even if not all are harvested
            if (publicationsData == null || publicationsData.Count == 0)
            {
                // Final attempt to get at least some data
                try
                {
                    publicationsData = await mediaService.GetBiblePublications(languageCode, "Music", downloadAll: false, progress);
                }
                catch (Exception ex)
                {
                    Serilog.Log.Error(ex, "PopulateSongPublications: Final fetch attempt failed for language={LanguageCode}",
                        languageCode);
                }
            }
        }

        return publicationsData;
    }
}

