using Bible.Alarm.Models.Enums;

namespace Bible.Alarm.Common.Helpers
{
    public static class SourceHelper
    {
        public static SourceWebsite GetSourceWebsite(string pubCode, bool isMusic = false)
        {
            if (isMusic)
            {
                return SourceWebsite.JwOrg;
            }

            switch (pubCode)
            {
                case "kjv":
                case "nivuk":
                    return SourceWebsite.BibleGateway;
                default:
                    return SourceWebsite.JwOrg;
            }

        }
    }
}
