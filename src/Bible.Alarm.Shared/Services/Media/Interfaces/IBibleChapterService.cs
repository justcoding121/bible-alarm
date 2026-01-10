#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Models.Media.Bible;

namespace Bible.Alarm.Shared.Services.Media.Interfaces;

/// <summary>
/// Service for accessing BiblePublicationChapter database operations.
/// </summary>
public interface IBibleChapterService : IDisposable
{
    /// <summary>
    /// Gets all BiblePublicationChapters for a given book (by language code, publication code, and book number), with Source included.
    /// </summary>
    Task<SortedDictionary<int, BiblePublicationChapter>> GetChaptersByBookAsync(string languageCode, string publicationCode, int bookNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a BiblePublicationChapter by language code, publication code, book number, and chapter number, with Source included.
    /// </summary>
    Task<BiblePublicationChapter?> GetChapterAsync(string languageCode, string publicationCode, int bookNumber, int chapterNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the URL for a Bible chapter's audio source.
    /// </summary>
    Task UpdateChapterUrlAsync(string languageCode, string publicationCode, int bookNumber, int chapterNumber, string url, CancellationToken cancellationToken = default);
}

