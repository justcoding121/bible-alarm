#nullable enable

namespace Bible.Alarm.Shared.Media;

/// <summary>
/// Parses a media id the same way the Android session callback does: a positive integer schedule id.
/// </summary>
public static class MediaSessionMediaId
{
    public static int ParseScheduleId(string? mediaId)
    {
        if (!int.TryParse(mediaId, out var scheduleId) || scheduleId <= 0)
        {
            return 0;
        }

        return scheduleId;
    }
}
