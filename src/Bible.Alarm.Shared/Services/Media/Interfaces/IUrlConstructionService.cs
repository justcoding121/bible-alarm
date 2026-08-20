#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Bible.Alarm.Shared.Services.Media.Interfaces;

/// <summary>
/// Service for constructing download URLs from the database using joins.
/// </summary>
public interface IUrlConstructionService
{
    /// <summary>
    /// Constructs download URLs for a Bible publication track.
    /// Returns both primary and backup URLs (one per base URL constant).
    /// </summary>
    /// <param name="trackId">The ID of the BiblePublicationTrack</param>
    /// <returns>List of constructed URLs (primary and backup)</returns>
    Task<List<string>> ConstructTrackUrlsAsync(int trackId);

    Task<List<string>> ConstructTrackUrlsAsync(
        string publicationCode,
        string languageCode,
        string? sectionCode,
        string trackCode);

    /// <summary>
    /// Constructs the lookup path (query string) for a track by publication code, language code, section code, and track number.
    /// Returns the query string part (e.g., "?output=json&pub=nwt&booknum=1&fileformat=MP3&langwritten=E&track=1").
    /// </summary>
    Task<string?> ConstructTrackLookUpPathAsync(
        string publicationCode,
        string? languageCode,
        string? sectionCode,
        string trackCode);

    /// <summary>
    /// Clears cached lookup-path/URL resolution so refreshed DB track URLs are picked up immediately.
    /// </summary>
    void ClearLookUpPathCache();
}
