using System.Collections.Generic;

namespace Bible.Alarm.Shared.Helpers;

public static class BgSourceHelper
{
    public static string GetBgSourceUrl(string languageCode, string publicationCode)
    {
        // Implementation for getting background source URL
        return $"https://b.jw-cdn.org/apis/pub-media/GETPUBMEDIALINKS?output=json&pub={publicationCode}&langwritten={languageCode}";
    }

    public static Dictionary<string, string> PublicationCodeToNameMappings => new()
    {
        { "nwt", "New World Translation" },
        { "bi12", "Bible Stories" },
        { "sjj", "Sing to Jehovah" },
        { "w", "Watchtower" },
        { "g", "Awake!" }
    };
}
