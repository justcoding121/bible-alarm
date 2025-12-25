#nullable enable
namespace Bible.Alarm.Stores.Actions.Playback;

/// <summary>
/// Action to set the default schedule metadata in state for Android Auto car play screen.
/// This is dispatched after fetching metadata from DefaultScheduleService.
/// </summary>
public class SetDefaultScheduleMetadataAction
{
    public int ScheduleId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Artist { get; init; } = string.Empty;
    public string? Album { get; init; }
    public string? ArtworkUrl { get; init; }
}

