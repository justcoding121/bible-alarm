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
    /// Gets the display name (pubName) for a video publication from GETPUBMEDIALINKS.
    /// Used for placeholders so flat video pubs (e.g. thv) show "Apply Yourself to Reading and Teaching—Videos" instead of the code.
    /// </summary>
    Task<string?> GetVideoPublicationDisplayNameAsync(
        string publicationCode,
        string languageCode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches all tracks for a flat-track publication (Music/Video - both use same GETPUBMEDIALINKS pattern).
    /// Deletes any existing data before inserting new.
    /// </summary>
    /// <param name="publicationCode">The publication code (e.g., "osg", "DramasGoodNews")</param>
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
    /// Progress is reported as each section is saved to DB (divided equally among sections).
    /// </summary>
    /// <param name="publicationCode">The publication code (e.g., "nwt", "Dramas")</param>
    /// <param name="languageCode">The language code (e.g., "MY", "A")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <param name="progress">Optional progress tracker for reporting per-section progress</param>
    /// <returns>True if successful, false otherwise</returns>
    Task<bool> FetchPublicationSectionsAsync(
        string publicationCode,
        string languageCode,
        IFetchProgress? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches tracks for a section. With <paramref name="replaceExistingTracksFromApi"/> false (default), skips the API when tracks already exist.
    /// With true, removes existing section tracks and re-fetches (e.g. after stale CDN URLs).
    /// </summary>
    /// <param name="publicationCode">The publication code (e.g., "nwt", "Dramas")</param>
    /// <param name="sectionCode">The section code (e.g., "1", "gen", section code for dramas)</param>
    /// <param name="languageCode">The language code (e.g., "MY", "A")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <param name="replaceExistingTracksFromApi">
    /// When true, removes existing section tracks and re-fetches from the API (e.g. stale CDN URLs).
    /// When false, skips fetch if the section already has tracks.
    /// </param>
    /// <returns>True if successful, false otherwise</returns>
    Task<bool> FetchSectionTracksAsync(
        string publicationCode,
        string sectionCode,
        string languageCode,
        bool replaceExistingTracksFromApi = false,
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

    /// <summary>
    /// Ensures a publication exists for the given language. If it doesn't exist, fetches it ad-hoc.
    /// Returns true if publication exists or was successfully fetched, false otherwise.
    /// </summary>
    /// <param name="publicationCode">The publication code (e.g., "nwt", "osg", "Dramas")</param>
    /// <param name="languageCode">The language code (e.g., "MY", "A")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if publication exists or was successfully fetched, false otherwise</returns>
    Task<bool> EnsurePublicationExistsAsync(
        string publicationCode,
        string languageCode,
        IFetchProgress? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches the first publication for a language with its first section and tracks (for sectioned publications)
    /// or all tracks (for flat publications). Used when language is first selected.
    /// </summary>
    /// <param name="languageCode">The language code (e.g., "MY", "A")</param>
    /// <param name="categoryName">Optional category name to filter publications (e.g., "Bible", "Music")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if first publication was successfully fetched, false otherwise</returns>
    Task<bool> FetchFirstPublicationForLanguageAsync(
        string languageCode,
        string? categoryName = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures all publications for a language are downloaded. Fetches missing publications with their first section and tracks.
    /// Used when publication list modal opens.
    /// </summary>
    /// <param name="languageCode">The language code (e.g., "MY", "A")</param>
    /// <param name="categoryName">Optional category name to filter publications (e.g., "Bible", "Music")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if all publications are now available, false otherwise</returns>
    Task<bool> EnsureAllPublicationsForLanguageAsync(
        string languageCode,
        string? categoryName = null,
        IFetchProgress? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures all sections for a publication are downloaded. Fetches missing sections.
    /// Used when sections modal opens.
    /// </summary>
    /// <param name="publicationCode">The publication code (e.g., "nwt", "Dramas")</param>
    /// <param name="languageCode">The language code (e.g., "MY", "A")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if all sections are now available, false otherwise</returns>
    Task<bool> EnsureAllSectionsForPublicationAsync(
        string publicationCode,
        string languageCode,
        IFetchProgress? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches only the first section when creating a new publication.
    /// This avoids fetching all sections when we only need the first one.
    /// Used when opening publication list modal - we only need first section for each publication.
    /// </summary>
    /// <param name="publicationCode">The publication code (e.g., "nwt", "Dramas")</param>
    /// <param name="firstSectionCode">The first section code to fetch</param>
    /// <param name="languageCode">The language code (e.g., "MY", "A")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if first section was successfully fetched, false otherwise</returns>
    Task<bool> FetchFirstSectionOnlyAsync(
        string publicationCode,
        string firstSectionCode,
        string languageCode,
        CancellationToken cancellationToken = default);
}
