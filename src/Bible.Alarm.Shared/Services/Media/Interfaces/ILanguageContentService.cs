#nullable enable
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Services.Media.Interfaces;

/// <summary>
/// Service for fetching and caching non-English language content on-demand.
/// When a user changes language, this service checks if the content exists in the database,
/// and if not, fetches it from the API and stores it for future use.
/// </summary>
public interface ILanguageContentService
{
    /// <summary>
    /// Fetches all tracks for a flat-track publication (Music/Video - both use same GETPUBMEDIALINKS pattern).
    /// Deletes any existing data before inserting new.
    /// </summary>
    /// <param name="publicationCode">The publication code (e.g., "osg", "gnj")</param>
    /// <param name="languageCode">The language code (e.g., "MY", "A")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if successful, false otherwise</returns>
    Task<bool> FetchPublicationTracksAsync(
        string publicationCode,
        string languageCode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches all sections for a publication that has sections (e.g., Bible, Drama).
    /// Deletes any existing sections before inserting new.
    /// </summary>
    /// <param name="publicationCode">The publication code (e.g., "nwt", "Dramas")</param>
    /// <param name="languageCode">The language code (e.g., "MY", "A")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if successful, false otherwise</returns>
    Task<bool> FetchPublicationSectionsAsync(
        string publicationCode,
        string languageCode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches all tracks for a specific section of a publication.
    /// Deletes any existing tracks for the section before inserting new.
    /// </summary>
    /// <param name="publicationCode">The publication code (e.g., "nwt", "Dramas")</param>
    /// <param name="sectionCode">The section code (e.g., "1", "gen", section code for dramas)</param>
    /// <param name="languageCode">The language code (e.g., "MY", "A")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if successful, false otherwise</returns>
    Task<bool> FetchSectionTracksAsync(
        string publicationCode,
        string sectionCode,
        string languageCode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches and creates English publication from scratch (for initial seeding).
    /// Determines category from publication code and uses discovered languages table.
    /// </summary>
    /// <param name="publicationCode">The publication code (e.g., "nwt", "osg", "Dramas")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if successful, false otherwise</returns>
    Task<bool> SeedEnglishPublicationAsync(
        string publicationCode,
        CancellationToken cancellationToken = default);
}
