#nullable enable

using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Shared.Models.Media;
using CommunityToolkit.Mvvm.Messaging;
using Serilog;

namespace Bible.Alarm.Services.Media.Playback;

/// <summary>
/// Handles on-demand track preparation (ensure current track is ready) and background pre-download of next track.
/// </summary>
public sealed class TrackOnDemandPreparer
{
    private readonly IPreparePlaybackService preparePlaybackService;
    private readonly ILogger logger;

    public TrackOnDemandPreparer(IPreparePlaybackService preparePlaybackService, ILogger logger)
    {
        this.preparePlaybackService = preparePlaybackService;
        this.logger = logger;
    }

    public async Task<bool> EnsureTrackPreparedAsync(AudioPlayerTrack track, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(track.Uri))
        {
            SendProgress(loadedTracks: 1, totalTracks: 1, bytesDownloaded: 1, totalBytes: 1);
            return true;
        }

        try
        {
            SendProgress(loadedTracks: 0, totalTracks: 1, bytesDownloaded: 0, totalBytes: null);

            var prepared = await preparePlaybackService.PrepareSingleTrackWithProgressAsync(
                track.PlayItem,
                (bytesDownloaded, totalBytes) => SendProgress(loadedTracks: 0, totalTracks: 1, bytesDownloaded: bytesDownloaded, totalBytes: totalBytes),
                cancellationToken);

            if (prepared == null || string.IsNullOrEmpty(prepared.Uri))
            {
                return false;
            }

            track.Uri = prepared.Uri;
            SendProgress(loadedTracks: 1, totalTracks: 1, bytesDownloaded: 1, totalBytes: 1);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to prepare track on-demand: {Url}", track.PlayItem?.Url ?? "Unknown");
            return false;
        }
    }

    public async Task PreDownloadNextTrackAsync(
        IReadOnlyList<AudioPlayerTrack> playlist,
        int currentIndex,
        bool isIndefinitePlayback,
        Func<Task<bool>> tryAppendNextTrack,
        CancellationToken cancellationToken)
    {
        if (playlist.Count == 0)
        {
            return;
        }

        var nextIndex = currentIndex + 1;
        if (nextIndex < 0 || nextIndex >= playlist.Count)
        {
            if (isIndefinitePlayback)
            {
                var appended = await tryAppendNextTrack();
                if (!appended)
                {
                    return;
                }

                nextIndex = currentIndex + 1;
                if (nextIndex < 0 || nextIndex >= playlist.Count)
                {
                    return;
                }
            }
            else
            {
                return;
            }
        }

        var nextTrack = playlist[nextIndex];
        if (!string.IsNullOrEmpty(nextTrack.Uri))
        {
            return;
        }

        try
        {
            var prepared = await preparePlaybackService.PrepareSingleTrackAsync(nextTrack.PlayItem, cancellationToken);
            if (prepared != null && !string.IsNullOrEmpty(prepared.Uri))
            {
                nextTrack.Uri = prepared.Uri;
            }
        }
        catch (OperationCanceledException)
        {
            // Ignore.
        }
        catch (Exception ex)
        {
            logger.Debug(ex, "Background pre-download for next track failed");
        }
    }

    private static void SendProgress(int loadedTracks, int totalTracks, long bytesDownloaded, long? totalBytes)
    {
        WeakReferenceMessenger.Default.Send(new PlaybackPreparationProgressMessage
        {
            LoadedTracks = loadedTracks,
            TotalTracks = totalTracks,
            CurrentTrackProgress = 0.0,
            BytesDownloaded = bytesDownloaded,
            TotalBytes = totalBytes,
            TotalBytesDownloaded = bytesDownloaded,
            TotalBytesExpected = totalBytes
        });
    }
}
