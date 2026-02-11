#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Services.Media.Playback;

/// <summary>
/// Initializes session navigation context (anchor Bible metadata, pre-anchor, session music) for indefinite playback.
/// </summary>
public sealed class PlaybackSessionContextInitializer
{
    private readonly IPlaylistService playlistService;

    public PlaybackSessionContextInitializer(IPlaylistService playlistService)
    {
        this.playlistService = playlistService;
    }

    public async Task InitializeAsync(
        IReadOnlyList<AudioPlayerTrack> playlist,
        Action<PlayItem?> setSessionMusicPlayItem,
        Action<TrackMetadata?> setAnchorBibleMetadata,
        Action<TrackMetadata?> setPreAnchorBibleMetadata)
    {
        if (playlist == null || playlist.Count == 0)
        {
            setSessionMusicPlayItem(null);
            setAnchorBibleMetadata(null);
            setPreAnchorBibleMetadata(null);
            return;
        }

        var sessionMusic = playlist.FirstOrDefault(t => t.PlayItem.Metadata.PlayType == PlayType.Music)?.PlayItem;
        setSessionMusicPlayItem(sessionMusic);

        var anchorBible = playlist.FirstOrDefault(t => t.PlayItem.Metadata.PlayType == PlayType.Bible)?.PlayItem.Metadata;
        setAnchorBibleMetadata(anchorBible);

        if (anchorBible != null)
        {
            try
            {
                var preAnchor = await playlistService.GetPreviousPlayItemAsync(anchorBible);
                setPreAnchorBibleMetadata(preAnchor.Metadata);
            }
            catch
            {
                setPreAnchorBibleMetadata(null);
            }
        }
        else
        {
            setPreAnchorBibleMetadata(null);
        }
    }
}
