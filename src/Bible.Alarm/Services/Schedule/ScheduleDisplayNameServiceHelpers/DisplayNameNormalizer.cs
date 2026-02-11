#nullable enable

using System.Net;

namespace Bible.Alarm.Services.Schedule.ScheduleDisplayNameServiceHelpers;

public static class DisplayNameNormalizer
{
    public static string? NormalizeTrackTitle(string? rawTitle)
    {
        if (string.IsNullOrWhiteSpace(rawTitle))
            return null;

        var decoded = WebUtility.HtmlDecode(rawTitle).Replace('\u00A0', ' ').Trim();
        if (decoded.Length == 0)
            return null;

        return decoded;
    }
}
