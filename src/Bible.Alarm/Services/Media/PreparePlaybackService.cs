#nullable enable
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Models.Media;
using Serilog;

namespace Bible.Alarm.Services.Media;

public class PreparePlaybackService : IPreparePlaybackService
{
    private readonly ILogger _logger;
    private readonly IPlaylistService _playlistService;
    private readonly IMediaCacheService _cacheService;

    public PreparePlaybackService(
        ILogger logger,
        IPlaylistService playlistService,
        IMediaCacheService cacheService)
    {
        _logger = logger;
        _playlistService = playlistService;
        _cacheService = cacheService;
    }

    public async Task<List<AudioPlayerTrack>> PrepareTracksAsync(int scheduleId)
    {
        var playItems = await _playlistService.NextTracks(scheduleId);
        var preparedTracks = new List<AudioPlayerTrack>();

        foreach (var playItem in playItems)
        {
            string uri;
            
            if (await _cacheService.Exists(playItem.Url))
            {
                var cachePath = _cacheService.GetCacheFilePath(playItem.Url);
                uri = new Uri(cachePath).AbsoluteUri;
            }
            else
            {
                uri = playItem.Url;
            }

            preparedTracks.Add(new AudioPlayerTrack
            {
                Uri = uri,
                PlayItem = playItem
            });
        }

        return preparedTracks;
    }
}

