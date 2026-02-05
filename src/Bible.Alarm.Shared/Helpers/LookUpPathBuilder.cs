#nullable enable
using System;

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
    /// <param name="sectionCode">The section code (e.g., "1" for Bible books). Null/empty means non-sectioned (flat music uses MP3, videos use MP4).</param>
    /// <param name="trackNumber">The track number</param>
    /// <param name="isNoLanguagePublication">True if the publication has no language (e.g., instrumental music). Defaults to false.</param>
    /// <returns>The lookup path query string</returns>
    public static string BuildBiblePublicationTrackLookUpPath(
        string languageCode,
        string publicationCode,
        string? sectionCode,
        int trackNumber,
        bool isNoLanguagePublication = false)
    {
        // Melody disc-style codes (e.g., "iam-9") are NOT Bible book numbers.
        // JW API expects the disc code as the publication code, without booknum:
        //   ?output=json&pub=iam-9&fileformat=MP3&langwritten=E&track=17
        // If section code follows the pattern "publicationCode-*", it's a disc-style section
        // which implies a no-language publication
        var isDiscStyleSection = !string.IsNullOrWhiteSpace(sectionCode) &&
            sectionCode.Contains('-') &&
            sectionCode.StartsWith(publicationCode + "-", StringComparison.OrdinalIgnoreCase);

        // No-language publications (including disc-style sections) always use "E"
        var effectiveIsNoLanguage = isNoLanguagePublication || isDiscStyleSection;
        var lc = effectiveIsNoLanguage || string.IsNullOrWhiteSpace(languageCode) ? "E" : languageCode.Trim();

        // For non-sectioned publications: flat music (e.g. osg) uses MP3; videos use MP4
        if (string.IsNullOrWhiteSpace(sectionCode))
        {
            // Vocal music (osg, sjjc, etc.) is flat and MP3. Harvester uses fileformat=MP3 for these.
            if (JwSourceHelper.VocalMusicPublicationCodes.Contains(publicationCode))
            {
                return $"?output=json&pub={publicationCode}&fileformat=MP3&langwritten={lc}&track={trackNumber}";
            }
            // Videos use MP4, not MP3. Match harvester format: no txtCMSLang for videos
            return $"?output=json&pub={publicationCode}&fileformat=MP4&langwritten={lc}&track={trackNumber}";
        }

        // For disc-style sections, use the section code as the pub parameter
        if (isDiscStyleSection)
        {
            return $"?output=json&pub={sectionCode}&fileformat=MP3&langwritten={lc}&track={trackNumber}";
        }

        // For sectioned Bible publications, use booknum (not sectionnum)
        // Match harvester format: ?output=json&pub={publicationCode}&booknum={sectionCode}&fileformat=MP3&alllangs=0&langwritten={languageCode}
        // Note: We include track parameter for individual track lookup, harvester doesn't use it when fetching all tracks
        return $"?output=json&pub={publicationCode}&booknum={sectionCode}&fileformat=MP3&langwritten={lc}&track={trackNumber}";
    }

    /// <summary>
    /// Builds a lookup path for a music track.
    /// </summary>
    /// <param name="publicationCode">The publication code (e.g., "osg", "sjj", "iam")</param>
    /// <param name="languageCode">The language code, or null for melody music (defaults to "E")</param>
    /// <param name="trackNumber">The track number</param>
    /// <param name="downloadCode">Optional download code (e.g., "iam-1", "iam-2" for melody music discs). If provided, this is used instead of publicationCode.</param>
    /// <param name="originalTrackNumber">Optional original track number from API (within the disc). For melody music with discs, this should be the track number within that specific disc, not the sequential number across all discs.</param>
    /// <param name="isNoLanguagePublication">True if the publication has no language (e.g., instrumental music). Defaults to false. When true, langwritten=E is always used.</param>
    /// <returns>The lookup path query string</returns>
    public static string BuildMusicTrackLookUpPath(
        string publicationCode,
        string? languageCode,
        int trackNumber,
        string? downloadCode = null,
        int? originalTrackNumber = null,
        bool isNoLanguagePublication = false)
    {
        // Use downloadCode if provided (for melody music with discs), otherwise use publicationCode
        var pubCode = !string.IsNullOrEmpty(downloadCode) ? downloadCode : publicationCode;
        // Use originalTrackNumber if provided (for melody music with discs), otherwise use trackNumber
        var trackNum = originalTrackNumber ?? trackNumber;

        // If downloadCode is provided and follows disc pattern (e.g., "iam-1"), it's a no-language publication
        var isDiscStyleDownload = !string.IsNullOrEmpty(downloadCode) &&
            downloadCode.Contains('-') &&
            downloadCode.StartsWith(publicationCode + "-", StringComparison.OrdinalIgnoreCase);

        // No-language publications always use "E"
        var effectiveIsNoLanguage = isNoLanguagePublication || isDiscStyleDownload;
        var effectiveLanguageCode = effectiveIsNoLanguage ? "E" : languageCode;

        var langParam = string.IsNullOrEmpty(effectiveLanguageCode) ? "&langwritten=E" : $"&langwritten={effectiveLanguageCode}";
        return $"?output=json&pub={pubCode}&fileformat=MP3{langParam}&track={trackNum}";
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
