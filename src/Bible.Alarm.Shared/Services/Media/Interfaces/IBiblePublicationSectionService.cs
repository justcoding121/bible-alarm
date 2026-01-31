#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Services.Media.Interfaces;

/// <summary>
/// Service for accessing BiblePublicationSection database operations.
/// </summary>
public interface IBiblePublicationSectionService : IDisposable
{
    /// <summary>
    /// Gets the name of a Bible section by language code, publication code, and section number.
    /// </summary>
    Task<string?> GetSectionNameAsync(string languageCode, string publicationCode, int sectionCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all BiblePublicationSections for a given publication (by language code and publication code).
    /// </summary>
    Task<SortedDictionary<int, BiblePublicationSection>> GetSectionsByPublicationAsync(string languageCode, string publicationCode, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets sections for publications without language (LanguageId=null).
    /// This is data-driven and works for any publication with LanguageId=null, regardless of category.
    /// </summary>
    Task<SortedDictionary<int, BiblePublicationSection>> GetSectionsByPublicationWithoutLanguageAsync(string publicationCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a BiblePublicationSection by language code, publication code, and section number.
    /// </summary>
    Task<BiblePublicationSection?> GetSectionAsync(string languageCode, string publicationCode, int sectionCode, CancellationToken cancellationToken = default);
}

