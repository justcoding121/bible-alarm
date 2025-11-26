#nullable enable
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Models.Media;
using CommunityToolkit.Mvvm.Messaging;
using Serilog;

namespace Bible.Alarm.Services.Media;

public class PreparePlaybackService(
    ILogger logger,
    IPlaylistService playlistService,
    IMediaCacheService cacheService) : IPreparePlaybackService
{
    private readonly ILogger _logger = logger;
    private readonly IPlaylistService _playlistService = playlistService;
    private readonly IMediaCacheService _cacheService = cacheService;

    public async Task<List<AudioPlayerTrack>?> PrepareTracksAsync(int scheduleId)
    {
        var playItems = await _playlistService.NextTracks(scheduleId);
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
            var uri = await _cacheService.GetOrDownloadTrackUriAsync(playItem);
            
            if (uri == null)
            {
                _logger.Warning($"Failed to download {playItem.Url}");
                return null;
            }

            preparedTracks.Add(new AudioPlayerTrack
            {
                Uri = uri,
                PlayItem = playItem
            });

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
}

