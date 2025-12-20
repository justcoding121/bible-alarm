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

    public async Task<List<AudioPlayerTrack>?> PrepareTracksAsync(int scheduleId)
    {
        var playItems = await playlistService.NextTracks(scheduleId);
        var preparedTracks = new List<AudioPlayerTrack>();
        var totalTracks = playItems.Count;
        var loadedTracks = 0;

        // Send initial progress message with total count
        WeakReferenceMessenger.Default.Send(new PlaybackPreparationProgressMessage
        {
            LoadedTracks = 0,
            TotalTracks = totalTracks
        });

        foreach (var playItem in playItems)
        {
            var audioPlayerTrack = await PrepareSingleTrackAsync(playItem);

            if (audioPlayerTrack == null)
            {
                logger.Warning($"Failed to download {playItem.Url}");
                return null;
            }

            preparedTracks.Add(audioPlayerTrack);

            // Send progress update message after each track is prepared
            loadedTracks++;
            WeakReferenceMessenger.Default.Send(new PlaybackPreparationProgressMessage
            {
                LoadedTracks = loadedTracks,
                TotalTracks = totalTracks
            });

            // Add delay to ensure each progress state (1/3, 2/3, 3/3) is visible on UI
            if (loadedTracks < totalTracks)
            {
                await Task.Delay(50);
            }
        }

        return preparedTracks;
    }

    /// <summary>
    /// Prepares a single track by downloading it and creating an AudioPlayerTrack.
    /// Used for getting metadata for a single track without preparing the entire playlist.
    /// </summary>
    public async Task<AudioPlayerTrack?> PrepareSingleTrackAsync(PlayItem playItem)
    {
        var uri = await cacheService.GetOrDownloadTrackUriAsync(playItem);

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

