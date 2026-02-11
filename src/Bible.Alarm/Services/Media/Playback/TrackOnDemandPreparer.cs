#nullable enable

using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Shared.Models.Media;
using CommunityToolkit.Mvvm.Messaging;
using Serilog;

namespace Bible.Alarm.Services.Media.Playback;

/// <summary>
/// Handles on-demand track URI resolution (ensure current track has a playable URI).
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
            logger.Error(ex, "Failed to resolve track URI on-demand: {Url}", track.PlayItem?.Url ?? "Unknown");
            return false;
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
