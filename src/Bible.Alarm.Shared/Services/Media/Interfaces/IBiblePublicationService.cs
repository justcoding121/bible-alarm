#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Services.Media.Interfaces;

public interface IBiblePublicationService : IDisposable
{
    /// <summary>
    /// Gets a BiblePublication by language code and publication code, with Sections included.
    /// </summary>
    Task<BiblePublication?> GetByLanguageAndCodeWithSectionsAsync(string languageCode, string publicationCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a BiblePublication by language code and publication code, with Tracks included.
    /// </summary>
    Task<BiblePublication?> GetByLanguageAndCodeWithTracksAsync(string languageCode, string publicationCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all BiblePublications for a given language code, optionally filtered by category.
    /// When category is Music, set filterIsMusicWhenMusicCategory true only for music container (begin-with-music); false for Bible pub container so all Music category pubs are listed.
    /// </summary>
    Task<Dictionary<string, BiblePublication>> GetByLanguageCodeAsync(string languageCode, string? categoryName = null, bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all distinct Languages from BiblePublications, optionally filtered by category.
    /// When category is Music, set filterIsMusicWhenMusicCategory true only for music container; false for Bible pub container.
    /// </summary>
    Task<Dictionary<string, Language>> GetDistinctLanguagesAsync(string? categoryName = null, bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all available publication codes from PublicationLanguages for a given language and category.
    /// When category is Music, set filterIsMusicWhenMusicCategory true only for music container; false for Bible pub container.
    /// </summary>
    Task<List<string>> GetAvailablePublicationCodesAsync(string languageCode, string? categoryName = null, bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the first available publication code by ID order from PublicationLanguages for a given language and category.
    /// When category is Music, set filterIsMusicWhenMusicCategory true only for music container; false for Bible pub container.
    /// </summary>
    Task<string?> GetFirstPublicationCodeByOrderAsync(string languageCode, string? categoryName = null, bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a publication exists with no language (LanguageId == null).
    /// Used for instrumental music and other language-independent content.
    /// </summary>
    Task<bool> IsNoLanguagePublicationAsync(string publicationCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the primary category code and IsMusic for a publication (language-bound or no-language).
    /// Returns null if the publication is not found.
    /// </summary>
    Task<(string? CategoryCode, bool IsMusic)?> GetPublicationCategoryInfoAsync(string languageCode, string publicationCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets publication codes in category in display order (as in UI).
    /// Includes both language-bound publications for the given language and no-language publications in the category.
    /// </summary>
    Task<List<string>> GetPublicationCodesInCategoryOrderAsync(string languageCode, string categoryCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates cached publication data for the given language and code so the next load fetches from DB.
    /// Call after ad-hoc fetch (e.g. EnsurePublicationExistsAsync) so navigation sees the new data.
    /// </summary>
    void InvalidatePublicationCaches(string languageCode, string publicationCode);
}

