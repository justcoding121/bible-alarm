#nullable enable

using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media;
using CommunityToolkit.Mvvm.Messaging;
using Serilog;

namespace Bible.Alarm.Services.Media.Playback;

/// <summary>
/// Handles on-demand track URI resolution and background pre-downloading of upcoming tracks.
/// </summary>
public sealed class TrackOnDemandPreparer
{
    private readonly IPreparePlaybackService preparePlaybackService;
    private readonly IMediaCacheService mediaCacheService;
    private readonly ILogger logger;

    public TrackOnDemandPreparer(
        IPreparePlaybackService preparePlaybackService,
        IMediaCacheService mediaCacheService,
        ILogger logger)
    {
        this.preparePlaybackService = preparePlaybackService;
        this.mediaCacheService = mediaCacheService;
        this.logger = logger;
    }

    public async Task<bool> EnsureTrackPreparedAsync(AudioPlayerTrack track, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(track.Uri))
        {
            SendProgress(loadedTracks: 1, totalTracks: 1);
            return true;
        }

        try
        {
            SendProgress(loadedTracks: 0, totalTracks: 1);

            var prepared = await preparePlaybackService.PrepareSingleTrackAsync(
                track.PlayItem,
                cancellationToken);

            if (prepared == null || string.IsNullOrEmpty(prepared.Uri))
            {
                return false;
            }

            track.Uri = prepared.Uri;
            SendProgress(loadedTracks: 1, totalTracks: 1);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            logger.Error(ex, AppConstants.Logging.TrackOnDemandPreparerDiagnosticsLog.FailedToResolveTrackUriOnDemand, track.PlayItem?.Url ?? MediaTrackTitleHelper.UnknownTitle);
            return false;
        }
    }

    /// <summary>
    /// Pre-downloads the next track in the playlist to local cache (fire-and-forget).
    /// This improves UX (no buffering on next track), enables artwork extraction, and
    /// ensures the file is available for offline playback.
    /// </summary>
    public async Task PreDownloadNextTrackAsync(
        List<AudioPlayerTrack> playlist,
        int currentTrackIndex,
        CancellationToken cancellationToken)
    {
        var nextIndex = currentTrackIndex + 1;
        if (playlist == null || nextIndex >= playlist.Count)
        {
            return;
        }

        var nextTrack = playlist[nextIndex];
        var playItem = nextTrack.PlayItem;
        if (playItem == null)
        {
            return;
        }

        var scheduleId = (int)playItem.Metadata.ScheduleId;
        if (scheduleId <= 0)
        {
            return;
        }

        try
        {
            logger.Debug(AppConstants.Logging.TrackOnDemandPreparerDiagnosticsLog.PreDownloadingNextTrackBackground,
                playItem.Metadata.LookUpPath, playItem.Url);

            var cached = await mediaCacheService.CacheTrackAsync(playItem, scheduleId, cancellationToken);

            if (cached && string.IsNullOrEmpty(nextTrack.Uri))
            {
                // Resolve the URI now that the file is cached so playback can use the local file.
                var prepared = await preparePlaybackService.PrepareSingleTrackAsync(playItem, cancellationToken);
                if (prepared != null && !string.IsNullOrEmpty(prepared.Uri))
                {
                    nextTrack.Uri = prepared.Uri;
                    logger.Debug(AppConstants.Logging.TrackOnDemandPreparerDiagnosticsLog.PreDownloadCompleteUriResolved, prepared.Uri);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on playback stop / navigation.
        }
        catch (Exception ex)
        {
            // Non-critical: streaming fallback will handle playback.
            logger.Debug(ex, AppConstants.Logging.TrackOnDemandPreparerDiagnosticsLog.PreDownloadNextTrackFailedNonCritical);
        }
    }

    private static void SendProgress(int loadedTracks, int totalTracks)
    {
        WeakReferenceMessenger.Default.Send(new PlaybackPreparationProgressMessage
        {
            LoadedTracks = loadedTracks,
            TotalTracks = totalTracks,
            CurrentTrackProgress = 0.0,
            BytesDownloaded = loadedTracks,
            TotalBytes = totalTracks,
            TotalBytesDownloaded = loadedTracks,
            TotalBytesExpected = totalTracks
        });
    }
}
