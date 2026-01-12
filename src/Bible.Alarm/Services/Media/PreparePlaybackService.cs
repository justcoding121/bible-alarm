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
        var playItems = await playlistService.NextTracks(scheduleId);
        var preparedTracks = new List<AudioPlayerTrack>();
        var totalTracks = playItems.Count;
        var loadedTracks = 0;

        // Send initial progress message with total count
        SendProgressMessage(loadedTracks, totalTracks, 0, 0, null);

        foreach (var playItem in playItems)
        {
            // Check for cancellation before processing each track
            cancellationToken.ThrowIfCancellationRequested();

            // Capture current track index for progress callback
            var currentTrackIndex = loadedTracks;

            // Progress callback for download bytes
            void DownloadProgressCallback(long bytesDownloaded, long? totalBytes)
            {
                var trackProgress = totalBytes.HasValue && totalBytes.Value > 0
                    ? (double)bytesDownloaded / totalBytes.Value
                    : 0.0;
                SendProgressMessage(currentTrackIndex, totalTracks, trackProgress, bytesDownloaded, totalBytes);
            }

            var audioPlayerTrack = await PrepareSingleTrackWithProgressAsync(playItem, DownloadProgressCallback, cancellationToken);

            if (audioPlayerTrack == null)
            {
                logger.Warning($"Failed to download {playItem.Url}");
                return null;
            }

            preparedTracks.Add(audioPlayerTrack);

            // Send progress update message after each track is prepared
            loadedTracks++;
            SendProgressMessage(loadedTracks, totalTracks, 1.0, 0, null);

            // Add delay to ensure each progress state (1/3, 2/3, 3/3) is visible on UI
            // Use cancellation token for delay
            if (loadedTracks < totalTracks)
            {
                await Task.Delay(50, cancellationToken);
            }
        }

        return preparedTracks;
    }

    private static void SendProgressMessage(int loadedTracks, int totalTracks, double currentTrackProgress, long bytesDownloaded, long? totalBytes)
    {
        WeakReferenceMessenger.Default.Send(new PlaybackPreparationProgressMessage
        {
            LoadedTracks = loadedTracks,
            TotalTracks = totalTracks,
            CurrentTrackProgress = currentTrackProgress,
            BytesDownloaded = bytesDownloaded,
            TotalBytes = totalBytes
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


