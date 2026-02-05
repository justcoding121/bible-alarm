#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
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
        IFetchProgress? progress)
    {
        // Music type is inferred from LanguageCode: NULL/empty = instrumental, otherwise = vocal
        // For instrumental music, we need publications without language
        // For vocal music, we need publications with language (or both)
        Dictionary<string, BiblePublication>? publicationsData = null;
        
        // Check if this is instrumental music (no language code)
        var isMelodyMusic = string.IsNullOrEmpty(current?.LanguageCode) && string.IsNullOrEmpty(languageCode);

        if (isMelodyMusic)
        {
            // Instrumental music only - get publications without language FK
            // Use empty string as language code to get all Music category publications
            // GetBiblePublications will return publications with LanguageId == null for Music category
            publicationsData = await mediaService.GetBiblePublications(string.Empty, "Music", downloadAll, progress);

            // Filter to only publications without LanguageId
            if (publicationsData != null)
            {
                publicationsData = publicationsData
                    .Where(kvp => kvp.Value.LanguageId == null)
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            }
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
        if (downloadAll && !string.IsNullOrEmpty(languageCode) && !languageCode.Equals("E", StringComparison.OrdinalIgnoreCase))
        {
            publicationsData = await RetryFetchUntilHarvestedAsync(languageCode, progress);
        }

        // Final fallback if nothing came back
        if (publicationsData == null && !string.IsNullOrEmpty(languageCode))
        {
            publicationsData = await mediaService.GetBiblePublications(languageCode, "Music", downloadAll, progress);
        }

        return publicationsData;
    }

    private async Task<Dictionary<string, BiblePublication>?> RetryFetchUntilHarvestedAsync(string languageCode, IFetchProgress? progress)
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

        Serilog.Log.Information("PopulateSongPublications: Starting fetch with retries for language={LanguageCode}, category=Music",
            languageCode);

        Dictionary<string, BiblePublication>? publicationsData = null;

        while (!allHarvested && attempt < maxRetries && (DateTime.UtcNow - startTime) < maxWaitTime)
        {
            attempt++;

            try
            {
                // Fetch publications (this triggers harvesting if needed)
                // Pass progress to show download percentage during harvesting
                publicationsData = await mediaService.GetBiblePublications(languageCode, "Music", downloadAll: true, progress);

                // Wait a bit for background harvesting to start
                await Task.Delay(500);

                // Re-query to check if publications are now harvested (no progress needed for re-query)
                var reQueriedData = await mediaService.GetBiblePublications(languageCode, "Music", downloadAll: false, null);

                // Check if ALL publications are harvested (not placeholders)
                // A publication is harvested if it has a name that's different from its code and has an ID > 0
                var hasPublications = reQueriedData != null && reQueriedData.Values.Count > 0;
                var allHarvestedCheck = hasPublications && reQueriedData!.Values.All(p =>
                    !string.IsNullOrEmpty(p.Name) &&
                    p.Name != p.PublicationCode &&
                    p.Id > 0);

                if (allHarvestedCheck)
                {
                    publicationsData = reQueriedData;
                    allHarvested = true;
                    Serilog.Log.Information("PopulateSongPublications: All {Count} publications harvested on attempt {Attempt} for language={LanguageCode}",
                        publicationsData?.Count ?? 0, attempt, languageCode);
                }
                else
                {
                    // Log which publications are still placeholders for debugging
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
                        else if (!hasPublications)
                        {
                            Serilog.Log.Debug("PopulateSongPublications: Attempt {Attempt}: No publications found yet, will retry",
                                attempt);
                        }
                    }

                    // Wait with increasing delay before retrying (1s, 2s, 3s, etc., up to 5s)
                    var delay = Math.Min(retryDelay * attempt, 5000);
                    await Task.Delay(delay);
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "PopulateSongPublications: Attempt {Attempt} failed for language={LanguageCode}, will retry",
                    attempt, languageCode);

                // Wait before retrying on exception
                var delay = Math.Min(retryDelay * attempt, 5000);
                await Task.Delay(delay);
            }
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

