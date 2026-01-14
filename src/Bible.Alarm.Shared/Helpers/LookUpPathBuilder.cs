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
    /// <param name="sectionNumber">The Bible section number (1-66), or 0 for non-sectioned publications (videos)</param>
    /// <param name="trackNumber">The track number</param>
    /// <returns>The lookup path query string</returns>
    public static string BuildBiblePublicationTrackLookUpPath(string languageCode, string publicationCode, int sectionNumber, int trackNumber)
    {
        // For non-sectioned publications (videos), omit booknum parameter and use MP4
        if (sectionNumber == 0)
        {
            // Videos use MP4, not MP3. Match harvester format: no txtCMSLang for videos
            return $"?output=json&pub={publicationCode}&fileformat=MP4&langwritten={languageCode}&track={trackNumber}";
        }
        // For sectioned Bible publications, use booknum (not sectionnum) and txtCMSLang with languageCode (not E)
        // Match harvester format: ?output=json&pub={publicationCode}&booknum={sectionNumber}&fileformat=MP3&alllangs=0&langwritten={languageCode}&txtCMSLang={languageCode}
        // Note: We include track parameter for individual track lookup, harvester doesn't use it when fetching all tracks
        return $"?output=json&pub={publicationCode}&booknum={sectionNumber}&fileformat=MP3&langwritten={languageCode}&txtCMSLang={languageCode}&track={trackNumber}";
    }

    /// <summary>
    /// Builds a lookup path for a music track.
    /// </summary>
    /// <param name="publicationCode">The publication code (e.g., "osg", "sjj", "iam")</param>
    /// <param name="languageCode">The language code, or null for melody music (defaults to "E")</param>
    /// <param name="trackNumber">The track number</param>
    /// <param name="downloadCode">Optional download code (e.g., "iam-1", "iam-2" for melody music discs). If provided, this is used instead of publicationCode.</param>
    /// <param name="originalTrackNumber">Optional original track number from API (within the disc). For melody music with discs, this should be the track number within that specific disc, not the sequential number across all discs.</param>
    /// <returns>The lookup path query string</returns>
    public static string BuildMusicTrackLookUpPath(string publicationCode, string? languageCode, int trackNumber, string? downloadCode = null, int? originalTrackNumber = null)
    {
        // Use downloadCode if provided (for melody music with discs), otherwise use publicationCode
        var pubCode = !string.IsNullOrEmpty(downloadCode) ? downloadCode : publicationCode;
        // Use originalTrackNumber if provided (for melody music with discs), otherwise use trackNumber
        var trackNum = originalTrackNumber ?? trackNumber;
        // Match harvester format: ?output=json&pub={publicationDownloadCode}&fileformat=MP3&alllangs=0&langwritten={languageCode}&txtCMSLang={cmsLang}
        // Note: We include track parameter for individual track lookup, harvester doesn't use it when fetching all tracks
        var langParam = string.IsNullOrEmpty(languageCode) ? "&langwritten=E" : $"&langwritten={languageCode}";
        var cmsLang = languageCode ?? "E";
        return $"?output=json&pub={pubCode}&fileformat=MP3{langParam}&txtCMSLang={cmsLang}&track={trackNum}";
    }

    /// <summary>
    /// Builds a lookup path for a drama/video track using the Mediator API.
    /// </summary>
    /// <param name="categoryKey">The category key (e.g., "Dramas", "DramaticBibleReadings", "gnj")</param>
    /// <param name="languageCode">The language code (e.g., "E" for English)</param>
    /// <param name="trackNumber">The track number</param>
    /// <param name="naturalKey">Optional natural key for more precise lookup (e.g., "pub-dwj_E_1_AUDIO")</param>
    /// <returns>The lookup path query string for Mediator API</returns>
    public static string BuildDramaTrackLookUpPath(string categoryKey, string languageCode, int trackNumber, string? naturalKey = null)
    {
        if (!string.IsNullOrEmpty(naturalKey))
        {
            return $"?category={categoryKey}&lang={languageCode}&naturalKey={naturalKey}";
        }
        return $"?category={categoryKey}&lang={languageCode}&track={trackNumber}";
    }
}
