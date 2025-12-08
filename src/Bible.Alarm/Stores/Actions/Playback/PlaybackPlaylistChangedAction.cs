#nullable enable
namespace Bible.Alarm.Stores.Actions.Playback;

/// <summary>
/// Action dispatched when the playlist changes (new tracks loaded, track index changes).
/// Contains simplified track information for display in CarPlay/Android Auto.
/// </summary>
public class PlaybackPlaylistChangedAction
{
    public List<PlaylistTrackInfo>? Playlist { get; init; }
    public int CurrentTrackIndex { get; init; }
}

/// <summary>
/// Simplified track information for display in car interfaces.
/// </summary>
public class PlaylistTrackInfo
{
    public string Title { get; init; } = string.Empty;
    public string? Artist { get; init; }
    public string? Album { get; init; }
    public int Index { get; init; }
}
