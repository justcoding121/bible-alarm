#nullable enable
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Models.Media;
using Serilog;

namespace Bible.Alarm.Services.Media.Playback;

public sealed class TrackMarker
{
    private readonly IPlaylistService playlistService;
    private readonly ILogger logger;

    public TrackMarker(IPlaylistService playlistService, ILogger logger)
    {
        this.playlistService = playlistService;
        this.logger = logger;
    }

    public async Task MarkTrackAsPlayedAsync(List<AudioPlayerTrack>? playlist, int currentTrackIndex)
    {
        if (playlist == null || currentTrackIndex < 0 || currentTrackIndex >= playlist.Count)
        {
            return;
        }

        try
        {
            var track = playlist[currentTrackIndex];
            await playlistService.MarkTrackAsPlayed(track.PlayItem.Metadata);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error marking track as played");
        }
    }
}

