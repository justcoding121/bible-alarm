namespace Bible.Alarm.Stores.Actions.Playback;

public class PlaybackMetadataChangedAction
{
    public string? Title { get; init; }
    public string? Artist { get; init; }
    public string? Album { get; init; }
    public string? ArtworkUrl { get; init; }
}

