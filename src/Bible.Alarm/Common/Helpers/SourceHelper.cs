using Bible.Alarm.Shared.Models.Enums;

namespace Bible.Alarm.Common.Helpers;

public static class SourceHelper
{
    public static SourceWebsite GetSourceWebsite(string pubCode, bool isMusic = false)
    {
        return SourceWebsite.JwOrg;
    }
}