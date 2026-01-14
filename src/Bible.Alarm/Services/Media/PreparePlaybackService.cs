#nullable enable
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Models.Media;
using CommunityToolkit.Mvvm.Messaging;
using Serilog;

namespace Bible.Alarm.Services.Media;

public sealed class PreparePlaybackService(
    ILogger logger,
    IPlaylistService playlistService,
    IMediaCacheService cacheService,
    IDownloadService downloadService) : IPreparePlaybackService
{
    private const int MaxConcurrentDownloads = 3;

    public async Task<List<AudioPlayerTrack>?> PrepareTracksAsync(int scheduleId, CancellationToken cancellationToken = default)
    {
        // Send initial progress message BEFORE media lookup so alarm modal shows "Preparing.." immediately
        // We'll update the total tracks count once we know how many tracks we have
        SendProgressMessage(0, 1, 0, 0, null, 0, null);

        List<PlayItem> playItems;
        try
        {
            playItems = await playlistService.NextTracks(scheduleId);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to get track URLs (media lookup failed) for schedule {ScheduleId}", scheduleId);
            // Return null to signal error - PlaybackService will handle showing error message
            return null;
        }

        var totalTracks = playItems.Count;

        if (totalTracks == 0)
        {
            return new List<AudioPlayerTrack>();
        }

        // Update progress message with actual track count now that we have it
        SendProgressMessage(0, totalTracks, 0, 0, null, 0, null);

        // Phase 1: Get all Content-Length values first (HEAD requests in parallel)
        var trackSizes = new Dictionary<int, long?>();
        var sizeTasks = playItems.Select(async (playItem, index) =>
        {
            try
            {
                var contentLength = await downloadService.GetContentLengthAsync(playItem.Url, cancellationToken);
                lock (trackSizes)
                {
                    trackSizes[index] = contentLength;
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Failed to get Content-Length for track {Index}: {Url}", index, playItem.Url);
                lock (trackSizes)
                {
                    trackSizes[index] = null;
                }
            }
        });

        await Task.WhenAll(sizeTasks);

        // Calculate total expected bytes
        var totalBytesExpected = trackSizes.Values
            .Where(v => v.HasValue)
            .Sum(v => v!.Value);

        // Phase 2: Download tracks in parallel with concurrency limit
        var preparedTracks = new AudioPlayerTrack?[totalTracks]; // Use array to maintain order
        var trackProgress = new Dictionary<int, long>(); // Track index -> bytes downloaded
        var progressLock = new object();
        var loadedTracks = 0;
        var loadedTracksLock = new object();

        using var semaphore = new SemaphoreSlim(MaxConcurrentDownloads, MaxConcurrentDownloads);

        var downloadTasks = playItems.Select(async (playItem, index) =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                // Progress callback for this specific track
                void DownloadProgressCallback(long bytesDownloaded, long? totalBytes)
                {
                    lock (progressLock)
                    {
                        // Update this track's progress
                        trackProgress[index] = bytesDownloaded;

                        // Calculate overall progress
                        var totalBytesDownloaded = trackProgress.Values.Sum();

                        // Send progress message with overall stats
                        // Note: currentTrackProgress, bytesDownloaded, and totalBytes are only used as fallback
                        // Since we always have overall progress from Phase 1, these can be simplified
                        SendProgressMessage(
                            loadedTracks,
                            totalTracks,
                            0.0, // Not used when overall progress is available
                            bytesDownloaded,
                            totalBytes,
                            totalBytesDownloaded,
                            totalBytesExpected > 0 ? totalBytesExpected : null);
                    }
                }

                var audioPlayerTrack = await PrepareSingleTrackWithProgressAsync(playItem, DownloadProgressCallback, cancellationToken);

                if (audioPlayerTrack == null)
                {
                    logger.Warning("Failed to download track {Index}: {Url}", index, playItem.Url);
                    return null;
                }

                lock (loadedTracksLock)
                {
                    preparedTracks[index] = audioPlayerTrack; // Store in correct position
                    loadedTracks++;
                }

                // Update progress after track completion - ensure we use the final size
                lock (progressLock)
                {
                    // Set final size for this track (use actual size if known, otherwise keep current progress)
                    if (trackSizes[index].HasValue)
                    {
                        trackProgress[index] = trackSizes[index]!.Value;
                    }
                    
                    var totalBytesDownloaded = trackProgress.Values.Sum();
                    SendProgressMessage(
                        loadedTracks,
                        totalTracks,
                        0.0, // Not used when overall progress is available
                        trackSizes[index] ?? trackProgress.GetValueOrDefault(index, 0),
                        trackSizes[index],
                        totalBytesDownloaded,
                        totalBytesExpected > 0 ? totalBytesExpected : null);
                }

                return audioPlayerTrack;
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(downloadTasks);

        // Check if any downloads failed
        if (preparedTracks.Any(r => r == null))
        {
            logger.Warning("Some tracks failed to download");
            return null;
        }

        return preparedTracks.Where(t => t != null).ToList()!;
    }

    private static void SendProgressMessage(int loadedTracks, int totalTracks, double currentTrackProgress, long bytesDownloaded, long? totalBytes, long totalBytesDownloaded, long? totalBytesExpected)
    {
        WeakReferenceMessenger.Default.Send(new PlaybackPreparationProgressMessage
        {
            LoadedTracks = loadedTracks,
            TotalTracks = totalTracks,
            CurrentTrackProgress = currentTrackProgress,
            BytesDownloaded = bytesDownloaded,
            TotalBytes = totalBytes,
            TotalBytesDownloaded = totalBytesDownloaded,
            TotalBytesExpected = totalBytesExpected
        });
    }

    /// <summary>
    /// Prepares a single track by downloading it and creating an AudioPlayerTrack.
    /// Used for getting metadata for a single track without preparing the entire playlist.
    /// </summary>
    public async Task<AudioPlayerTrack?> PrepareSingleTrackAsync(PlayItem playItem, CancellationToken cancellationToken = default)
    {
        return await PrepareSingleTrackWithProgressAsync(playItem, null, cancellationToken);
    }

    private async Task<AudioPlayerTrack?> PrepareSingleTrackWithProgressAsync(PlayItem playItem, Action<long, long?>? progressCallback, CancellationToken cancellationToken = default)
    {
        var uri = await cacheService.GetOrDownloadTrackUriWithProgressAsync(playItem, progressCallback, cancellationToken);

        if (uri == null)
        {
            logger.Warning("Failed to download track: {Url}", playItem.Url);
            return null;
        }

        return new AudioPlayerTrack
        {
            Uri = uri,
            PlayItem = playItem
        };
    }

}


