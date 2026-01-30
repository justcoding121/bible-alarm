#nullable enable
namespace Bible.Alarm.Shared.Models.Media;

public class AudioPlayerTrack
{
    // Uri is populated when the track is downloaded/prepared.
    // It may be set later (e.g., background pre-download) after the playlist is created.
    public string Uri { get; set; } = string.Empty;
    public PlayItem PlayItem { get; init; } = null!;
}

