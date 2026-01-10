#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.Bible;

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
    /// Gets all BiblePublications for a given language code.
    /// </summary>
    Task<Dictionary<string, BiblePublication>> GetByLanguageCodeAsync(string languageCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all distinct Languages from BiblePublications.
    /// </summary>
    Task<Dictionary<string, Language>> GetDistinctLanguagesAsync(CancellationToken cancellationToken = default);
}

