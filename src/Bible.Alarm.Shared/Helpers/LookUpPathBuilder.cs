#nullable enable

namespace Bible.Alarm.Shared.Helpers;

/// <summary>
/// Builds lookup paths for refreshing audio URLs from the JW.org API.
/// This replaces storing LookUpPath in the database - it can be calculated from known parameters.
/// </summary>
public static class LookUpPathBuilder
{
    /// <summary>
    /// Builds a lookup path for a Bible track.
    /// </summary>
    /// <param name="languageCode">The language code (e.g., "E" for English)</param>
    /// <param name="publicationCode">The publication/publication code (e.g., "nwt")</param>
    /// <param name="sectionNumber">The Bible section number (1-66)</param>
    /// <param name="trackNumber">The track number</param>
    /// <returns>The lookup path query string</returns>
    public static string BuildBiblePublicationTrackLookUpPath(string languageCode, string publicationCode, int sectionNumber, int trackNumber)
    {
        return $"?output=json&pub={publicationCode}&fileformat=MP3&langwritten={languageCode}&txtCMSLang=E&sectionnum={sectionNumber}&track={trackNumber}";
    }

    /// <summary>
    /// Builds a lookup path for a music track.
    /// </summary>
    /// <param name="publicationCode">The publication code (e.g., "osg", "sjj")</param>
    /// <param name="languageCode">The language code, or null for melody music (defaults to "E")</param>
    /// <param name="trackNumber">The track number</param>
    /// <returns>The lookup path query string</returns>
    public static string BuildMusicTrackLookUpPath(string publicationCode, string? languageCode, int trackNumber)
    {
        var langParam = string.IsNullOrEmpty(languageCode) ? "&langwritten=E" : $"&langwritten={languageCode}";
        return $"?output=json&pub={publicationCode}&fileformat=MP3{langParam}&txtCMSLang=E&track={trackNumber}";
    }
}
