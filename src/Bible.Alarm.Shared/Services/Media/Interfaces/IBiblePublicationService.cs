#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Services.Media.Interfaces;

/// <summary>
/// Service for accessing BiblePublication database operations.
/// </summary>
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
    /// </summary>
    Task<Dictionary<string, BiblePublication>> GetByLanguageCodeAsync(string languageCode, string? categoryName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all distinct Languages from BiblePublications, optionally filtered by category.
    /// </summary>
    Task<Dictionary<string, Language>> GetDistinctLanguagesAsync(string? categoryName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all available publication codes from PublicationLanguages for a given language and category.
    /// This shows all discovered publications, even if not yet downloaded.
    /// </summary>
    Task<List<string>> GetAvailablePublicationCodesAsync(string languageCode, string? categoryName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the first available publication code by ID order from PublicationLanguages for a given language and category.
    /// Used for cascade downloading when a language is selected.
    /// </summary>
    Task<string?> GetFirstPublicationCodeByOrderAsync(string languageCode, string? categoryName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a publication exists with no language (LanguageId == null).
    /// Used for instrumental music and other language-independent content.
    /// </summary>
    Task<bool> IsNoLanguagePublicationAsync(string publicationCode, CancellationToken cancellationToken = default);
}

