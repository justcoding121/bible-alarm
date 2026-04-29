#nullable enable
using System;
using Bible.Alarm.Shared.Constants;

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
    /// <param name="trackCode">The track number</param>
    /// <param name="isNoLanguagePublication">True if the publication has no language (e.g., instrumental music). Defaults to false.</param>
    /// <returns>The lookup path query string</returns>
    public static string BuildBiblePublicationTrackLookUpPath(
        string languageCode,
        string publicationCode,
        string? sectionCode,
        string trackCode,
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
        var lc = effectiveIsNoLanguage || string.IsNullOrWhiteSpace(languageCode) ? AppConstants.Media.DefaultLanguageCode : languageCode.Trim();

        // Section-level or pub-level only (no track=). Refetch returns all tracks; resolve by trackCode in response.
        if (string.IsNullOrWhiteSpace(sectionCode))
        {
            if (JwSourceHelper.VocalMusicPublicationCodes.Contains(publicationCode))
                return $"?{AppConstants.Media.GetPubQueryOutputJson}&{AppConstants.Media.GetPubQueryParamName.Pub}={publicationCode}&{AppConstants.Media.GetPubQueryParamName.FileFormat}={AppConstants.Media.MediaStreamFormatMp3}&{AppConstants.Media.GetPubQueryParamLangWritten}={lc}";
            return $"?{AppConstants.Media.GetPubQueryOutputJson}&{AppConstants.Media.GetPubQueryParamName.Pub}={publicationCode}&{AppConstants.Media.GetPubQueryParamName.FileFormat}={AppConstants.Media.MediaStreamFormatMp4}&{AppConstants.Media.GetPubQueryParamLangWritten}={lc}";
        }

        if (isDiscStyleSection)
            return $"?{AppConstants.Media.GetPubQueryOutputJson}&{AppConstants.Media.GetPubQueryParamName.Pub}={sectionCode}&{AppConstants.Media.GetPubQueryParamName.FileFormat}={AppConstants.Media.MediaStreamFormatMp3}&{AppConstants.Media.GetPubQueryParamLangWritten}={lc}";

        return $"?{AppConstants.Media.GetPubQueryOutputJson}&{AppConstants.Media.GetPubQueryParamName.Pub}={publicationCode}&{AppConstants.Media.GetPubQueryParamName.BookNum}={sectionCode}&{AppConstants.Media.GetPubQueryParamName.FileFormat}={AppConstants.Media.MediaStreamFormatMp3}&{AppConstants.Media.GetPubQueryAllLangsOff}&{AppConstants.Media.GetPubQueryParamLangWritten}={lc}";
    }

    /// <summary>
    /// Builds a lookup path for a music track.
    /// </summary>
    /// <param name="publicationCode">The publication code (e.g., "osg", "sjj", "iam")</param>
    /// <param name="languageCode">The language code, or null for melody music (defaults to "E")</param>
    /// <param name="trackCode">The track number</param>
    /// <param name="downloadCode">Optional download code (e.g., "iam-1", "iam-2" for melody music discs). If provided, this is used instead of publicationCode.</param>
    /// <param name="originalTrackCode">Optional original track number from API (within the disc). For melody music with discs, this should be the track number within that specific disc, not the sequential number across all discs.</param>
    /// <param name="isNoLanguagePublication">True if the publication has no language (e.g., instrumental music). Defaults to false. When true, langwritten=E is always used.</param>
    /// <returns>The lookup path query string</returns>
    public static string BuildMusicTrackLookUpPath(
        string publicationCode,
        string? languageCode,
        string trackCode,
        string? downloadCode = null,
        int? originalTrackCode = null,
        bool isNoLanguagePublication = false)
    {
        var pubCode = !string.IsNullOrEmpty(downloadCode) ? downloadCode : publicationCode;
        var isDiscStyleDownload = !string.IsNullOrEmpty(downloadCode) &&
            downloadCode.Contains('-') &&
            downloadCode.StartsWith(publicationCode + "-", StringComparison.OrdinalIgnoreCase);
        var effectiveIsNoLanguage = isNoLanguagePublication || isDiscStyleDownload;
        var effectiveLanguageCode = effectiveIsNoLanguage ? AppConstants.Media.DefaultLanguageCode : languageCode;
        var langParam = string.IsNullOrEmpty(effectiveLanguageCode) ? $"&{AppConstants.Media.GetPubQueryParamLangWritten}={AppConstants.Media.DefaultLanguageCode}" : $"&{AppConstants.Media.GetPubQueryParamLangWritten}={effectiveLanguageCode}";
        return $"?{AppConstants.Media.GetPubQueryOutputJson}&{AppConstants.Media.GetPubQueryParamName.Pub}={pubCode}&{AppConstants.Media.GetPubQueryParamName.FileFormat}={AppConstants.Media.MediaStreamFormatMp3}{langParam}";
    }

    /// <summary>
    /// Builds a lookup path for a mediator track using the Mediator API.
    /// </summary>
    /// <param name="categoryKey">The JW mediator category key (e.g. Dramas or MakingMusic).</param>
    /// <param name="languageCode">The language code (e.g., "E" for English)</param>
    /// <param name="trackCode">The track number</param>
    /// <param name="naturalKey">Optional natural key for more precise lookup (e.g., "pub-dwj_E_1_AUDIO")</param>
    /// <returns>The lookup path query string for Mediator API</returns>
    public static string BuildMediatorTrackLookUpPath(string categoryKey, string languageCode, string trackCode, string? naturalKey = null)
    {
        return $"?category={categoryKey}&lang={languageCode}";
    }
}
