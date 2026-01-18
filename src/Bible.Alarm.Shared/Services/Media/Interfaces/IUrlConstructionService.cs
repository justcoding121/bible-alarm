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
    /// Returns both primary and backup URLs (one for each BaseUrl linked to the publication).
    /// </summary>
    /// <param name="trackId">The ID of the BiblePublicationTrack</param>
    /// <returns>List of constructed URLs (primary and backup)</returns>
    Task<List<string>> ConstructTrackUrlsAsync(int trackId);

    /// <summary>
    /// Constructs download URLs for a track by publication code, language code, section number, and track number.
    /// </summary>
    Task<List<string>> ConstructTrackUrlsAsync(
        string publicationCode,
        string languageCode,
        int? sectionNumber,
        int trackNumber);

    /// <summary>
    /// Constructs the lookup path (query string) for a track by publication code, language code, section number, and track number.
    /// Returns the query string part (e.g., "?output=json&pub=nwt&booknum=1&fileformat=MP3&langwritten=E&track=1").
    /// Uses the first BaseUrl linked to the publication.
    /// </summary>
    Task<string?> ConstructTrackLookUpPathAsync(
        string publicationCode,
        string? languageCode,
        int? sectionNumber,
        int trackNumber);
}
