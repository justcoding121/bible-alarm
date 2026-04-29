#nullable enable

using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Services.Schedule.ScheduleDisplayNameServiceHelpers;

public static class DisplayNameNormalizer
{
    public static string? NormalizeTrackTitle(string? rawTitle)
    {
        if (string.IsNullOrWhiteSpace(rawTitle))
            return null;

        var decoded = MediaTrackTitleHelper.DecodeHtmlTitle(rawTitle).Trim();
        if (decoded.Length == 0)
            return null;

        return decoded;
    }
}
