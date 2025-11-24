#nullable enable
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Stores.Actions.Playback;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;
using Serilog;

namespace Bible.Alarm.Services.Media;

public class PreparePlaybackService : IPreparePlaybackService
{
    private readonly ILogger _logger;
    private readonly IPlaylistService _playlistService;
    private readonly IMediaCacheService _cacheService;
    private readonly IDispatcher _dispatcher;

    public PreparePlaybackService(
        ILogger logger,
        IPlaylistService playlistService,
        IMediaCacheService cacheService,
        IDispatcher dispatcher)
    {
        _logger = logger;
        _playlistService = playlistService;
        _cacheService = cacheService;
        _dispatcher = dispatcher;
    }

    public async Task<List<AudioPlayerTrack>?> PrepareTracksAsync(int scheduleId)
    {
        var playItems = await _playlistService.NextTracks(scheduleId);
        var preparedTracks = new List<AudioPlayerTrack>();
        var totalTracks = playItems.Count;
        var loadedTracks = 0;

        // Dispatch initial progress action with total count
        _dispatcher.Dispatch(new PlaybackPreparationProgressAction
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

            // Dispatch progress update after each track is prepared
            loadedTracks++;
            _dispatcher.Dispatch(new PlaybackPreparationProgressAction
            {
                LoadedTracks = loadedTracks,
                TotalTracks = totalTracks
            });
        }

        return preparedTracks;
    }
}

