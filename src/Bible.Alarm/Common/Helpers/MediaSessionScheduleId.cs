#nullable enable

namespace Bible.Alarm.Common.Helpers;

/// <summary>
/// Chooses the schedule id stored on the Android Auto media session.
/// Play on the car screen reads this id, so it must follow the schedule whose title and artwork are shown.
/// </summary>
public static class MediaSessionScheduleId
{
    public static string? Resolve(string? existingMediaId, int? scheduleId)
    {
        if (scheduleId is > 0)
        {
            return scheduleId.Value.ToString();
        }

        return string.IsNullOrEmpty(existingMediaId) ? null : existingMediaId;
    }

    public static bool IsScheduleChange(string? existingMediaId, int? scheduleId)
    {
        if (scheduleId is not > 0)
        {
            return false;
        }

        return existingMediaId != scheduleId.Value.ToString();
    }
}
