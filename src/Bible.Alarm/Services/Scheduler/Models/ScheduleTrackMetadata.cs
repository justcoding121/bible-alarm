#nullable enable
namespace Bible.Alarm.Services.Scheduler.Models;

/// <summary>
/// Metadata for the next schedule track to be displayed in Android Auto MediaSession.
/// </summary>
public class ScheduleTrackMetadata
{
    public int ScheduleId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Artist { get; init; } = string.Empty;
    public string? Album { get; init; }
    public string? ArtworkUrl { get; init; }
}

