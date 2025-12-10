#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.Bible;

namespace Bible.Alarm.Shared.Services.Media.Interfaces;

/// <summary>
/// Service for accessing BibleTranslation database operations.
/// </summary>
public interface IBibleTranslationService : IDisposable
{
    /// <summary>
    /// Gets a BibleTranslation by language code and publication code, with Books included.
    /// </summary>
    Task<BibleTranslation?> GetByLanguageAndCodeWithBooksAsync(string languageCode, string publicationCode, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all BibleTranslations for a given language code.
    /// </summary>
    Task<Dictionary<string, BibleTranslation>> GetByLanguageCodeAsync(string languageCode, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all distinct Languages from BibleTranslations.
    /// </summary>
    Task<Dictionary<string, Language>> GetDistinctLanguagesAsync(CancellationToken cancellationToken = default);
}

