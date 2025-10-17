using System.Collections.Generic;
using System.Linq;

namespace Bible.Alarm.Common.Helpers
{
    public static class JwSourceHelper
    {
        public static Dictionary<string, string> PublicationCodeToNameMappings =
                    new[]
                    {
                        new KeyValuePair<string, string>("nwt","New World Translation (2013)"),
                        new KeyValuePair<string, string>("bi12","New World Translation (1984)")

                    }.ToDictionary(x => x.Key, x => x.Value);

    }
}
