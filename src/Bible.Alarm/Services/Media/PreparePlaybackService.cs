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
    IMediaCacheService cacheService) : IPreparePlaybackService
{
    public async Task<List<AudioPlayerTrack>?> PrepareTracksAsync(int scheduleId, CancellationToken cancellationToken = default)
    {
        // Always ensure the *first* track that needs to play is downloaded before starting playback.
        // Remaining tracks (finite mode) can be pre-downloaded in the background.

        // Send initial progress message BEFORE media lookup so alarm modal shows "Preparing.." immediately.
        // We always represent the blocking work as 1 track (the first one).
        SendProgressMessage(0, 1, 0, 0, null, 0, null);

        List<PlayItem> playItems;
        try
        {
            playItems = await playlistService.NextTracks(scheduleId);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to get track URLs (media lookup failed) for schedule {ScheduleId}", scheduleId);
            return null;
        }

        if (playItems.Count == 0)
        {
            // Nothing to play.
            SendProgressMessage(1, 1, 0, 1, 1, 1, 1);
            return new List<AudioPlayerTrack>();
        }

        // Build playlist immediately; tracks will get their Uri filled as they download.
        var tracks = playItems
            .Select(pi => new AudioPlayerTrack { PlayItem = pi, Uri = string.Empty })
            .ToList();

        // Download/prepare the first track with progress.
        AudioPlayerTrack? firstPrepared;
        try
        {
            firstPrepared = await PrepareSingleTrackWithProgressAsync(
                playItems[0],
                (bytesDownloaded, totalBytes) =>
                {
                    // Byte-based progress for the single blocking track.
                    SendProgressMessage(
                        loadedTracks: 0,
                        totalTracks: 1,
                        currentTrackProgress: 0.0,
                        bytesDownloaded: bytesDownloaded,
                        totalBytes: totalBytes,
                        totalBytesDownloaded: bytesDownloaded,
                        totalBytesExpected: totalBytes);
                },
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to prepare first track for schedule {ScheduleId}", scheduleId);
            return null;
        }

        if (firstPrepared == null || string.IsNullOrEmpty(firstPrepared.Uri))
        {
            logger.Warning("Failed to download first track for schedule {ScheduleId}", scheduleId);
            return null;
        }

        tracks[0].Uri = firstPrepared.Uri;

        // Signal preparation complete (1/1) so the modal can transition out of preparing state.
        SendProgressMessage(1, 1, 0.0, 1, 1, 1, 1);

        // Background: pre-download remaining tracks through cache logic.
        // This should not block playback.
        _ = Task.Run(async () =>
        {
            try
            {
                await PreDownloadRemainingTracksAsync(tracks, startIndex: 1, cancellationToken);
            }
            catch
            {
                // Swallow background errors - on-demand download will still work when the track is requested.
            }
        }, CancellationToken.None);

        return tracks;
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

    public async Task<AudioPlayerTrack?> PrepareSingleTrackWithProgressAsync(PlayItem playItem, Action<long, long?>? progressCallback, CancellationToken cancellationToken = default)
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

    private async Task PreDownloadRemainingTracksAsync(List<AudioPlayerTrack> tracks, int startIndex, CancellationToken cancellationToken)
    {
        if (startIndex >= tracks.Count)
        {
            return;
        }

        const int maxConcurrent = 3;
        using var semaphore = new SemaphoreSlim(maxConcurrent, maxConcurrent);

        var tasks = new List<Task>(Math.Max(0, tracks.Count - startIndex));
        for (var i = startIndex; i < tracks.Count; i++)
        {
            var index = i;
            tasks.Add(Task.Run(async () =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    if (!string.IsNullOrEmpty(tracks[index].Uri))
                    {
                        return;
                    }

                    var uri = await cacheService.GetOrDownloadTrackUriAsync(tracks[index].PlayItem, cancellationToken);
                    if (!string.IsNullOrEmpty(uri))
                    {
                        tracks[index].Uri = uri;
                    }
                }
                catch (OperationCanceledException)
                {
                    // Ignore cancellation for background pre-download.
                }
                catch (Exception ex)
                {
                    logger.Debug(ex, "Background pre-download failed for track index {Index} (Url={Url})", index, tracks[index].PlayItem?.Url);
                }
                finally
                {
                    semaphore.Release();
                }
            }, CancellationToken.None));
        }

        await Task.WhenAll(tasks);
    }

}


