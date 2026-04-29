#nullable enable
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Shared.Constants;
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
        // Send initial progress message so alarm modal shows "Preparing.." immediately.
        SendProgressMessage(0, 1);

        List<PlayItem> playItems;
        try
        {
            playItems = await playlistService.NextTracks(scheduleId);
        }
        catch (Exception ex)
        {
            logger.Error(ex, AppConstants.Logging.PreparePlaybackServiceDiagnosticsLog.FailedToGetTrackUrlsMediaLookupFailed, scheduleId);
            return null;
        }

        if (playItems.Count == 0)
        {
            logger.Debug("[Playback] No play items for schedule {ScheduleId}", scheduleId);
            SendProgressMessage(1, 1);
            return new List<AudioPlayerTrack>();
        }

        if (playItems[0].Metadata is { } firstMeta)
        {
            logger.Information(AppConstants.Logging.PreparePlaybackServiceDiagnosticsLog.PreparingFirstTrackForSchedule,
                scheduleId, firstMeta.PublicationCode, firstMeta.SectionCode ?? "(null)", firstMeta.TrackCode, firstMeta.LookUpPath);
        }

        // Resolve URIs for all tracks (cache check + CDN URL fallback, no downloading).
        var tracks = new List<AudioPlayerTrack>(playItems.Count);
        foreach (var playItem in playItems)
        {
            var uri = await ResolveTrackUriAsync(playItem, cancellationToken);
            tracks.Add(new AudioPlayerTrack { PlayItem = playItem, Uri = uri ?? string.Empty });
        }

        // The first track must have a valid URI to start playback.
        if (string.IsNullOrEmpty(tracks[0].Uri))
        {
            logger.Warning("Failed to resolve first track URI for schedule {ScheduleId}", scheduleId);
            return null;
        }

        // Signal preparation complete so the modal can transition out of preparing state.
        SendProgressMessage(1, 1);

        return tracks;
    }

    private static void SendProgressMessage(int loadedTracks, int totalTracks)
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

    /// <summary>
    /// Resolves the URI for a single track (cached file or CDN URL for streaming).
    /// </summary>
    public async Task<AudioPlayerTrack?> PrepareSingleTrackAsync(PlayItem playItem, CancellationToken cancellationToken = default)
    {
        var uri = await ResolveTrackUriAsync(playItem, cancellationToken);

        if (uri == null)
        {
            logger.Warning(AppConstants.Logging.PreparePlaybackServiceDiagnosticsLog.FailedToResolveTrackUri, playItem.Url);
            return null;
        }

        return new AudioPlayerTrack
        {
            Uri = uri,
            PlayItem = playItem
        };
    }

    private async Task<string?> ResolveTrackUriAsync(PlayItem playItem, CancellationToken cancellationToken)
    {
        try
        {
            return await cacheService.ResolveTrackUriAsync(playItem, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            logger.Error(ex, AppConstants.Logging.PreparePlaybackServiceDiagnosticsLog.FailedToResolveTrackUri, playItem.Url);
            return null;
        }
    }
}
