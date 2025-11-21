#nullable enable
namespace Bible.Alarm.Shared.Models.Media;

public class AudioPlayerTrack
{
    public string Uri { get; init; } = string.Empty;
    public PlayItem PlayItem { get; init; } = null!;
}

