#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Models.Media.Bible;

namespace Bible.Alarm.Shared.Services.Media.Interfaces;

/// <summary>
/// Service for accessing BibleBook database operations.
/// </summary>
public interface IBibleBookService : IDisposable
{
    /// <summary>
    /// Gets the name of a Bible book by language code, publication code, and book number.
    /// </summary>
    Task<string?> GetBookNameAsync(string languageCode, string publicationCode, int bookNumber, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all BibleBooks for a given translation (by language code and publication code).
    /// </summary>
    Task<SortedDictionary<int, BibleBook>> GetBooksByTranslationAsync(string languageCode, string publicationCode, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets a BibleBook by language code, publication code, and book number.
    /// </summary>
    Task<BibleBook?> GetBookAsync(string languageCode, string publicationCode, int bookNumber, CancellationToken cancellationToken = default);
}

