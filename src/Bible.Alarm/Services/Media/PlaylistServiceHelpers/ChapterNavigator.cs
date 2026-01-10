#nullable enable
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Models.Media.Bible;
using Serilog;

namespace Bible.Alarm.Services.Media.PlaylistServiceHelpers;

/// <summary>
/// Handles Bible chapter and book navigation.
/// </summary>
public sealed class ChapterNavigator(IMediaService mediaService)
{
    /// <summary>
    /// Gets the next Bible chapter.
    /// </summary>
    public async Task<KeyValuePair<BibleBook, BiblePublicationChapter>> GetNextBiblePublicationChapter(
        string languageCode,
        string publicationCode,
        int bookNumber,
        int chapter)
    {
        var currentBook = await mediaService.GetBibleBook(languageCode, publicationCode, bookNumber) 
            ?? throw new InvalidOperationException($"Bible book not found: languageCode={languageCode}, publicationCode={publicationCode}, bookNumber={bookNumber}");
        
        var chapters = await mediaService.GetBiblePublicationChapters(languageCode, publicationCode, bookNumber);
        var nextChapter = chapters.SkipWhile(kvp => kvp.Key <= chapter).FirstOrDefault();

        if (!nextChapter.Equals(default(KeyValuePair<int, BiblePublicationChapter>)))
        {
            return new KeyValuePair<BibleBook, BiblePublicationChapter>(currentBook, nextChapter.Value);
        }

        var nextBook = await GetNextBibleBook(languageCode, publicationCode, bookNumber);
        if (nextBook.Value == null)
        {
            throw new InvalidOperationException($"Next bible book not found: languageCode={languageCode}, publicationCode={publicationCode}, bookNumber={bookNumber}");
        }

        chapters = await mediaService.GetBiblePublicationChapters(languageCode, publicationCode, nextBook.Key);
        if (chapters.Count == 0)
        {
            throw new InvalidOperationException($"No chapters in next book: languageCode={languageCode}, publicationCode={publicationCode}, bookNumber={nextBook.Key}");
        }

        // Start at the first chapter of the next book (index 0)
        return new KeyValuePair<BibleBook, BiblePublicationChapter>(nextBook.Value, chapters.ElementAt(0).Value);
    }

    /// <summary>
    /// Gets the previous Bible chapter.
    /// </summary>
    public async Task<KeyValuePair<BibleBook, BiblePublicationChapter>> GetPreviousBiblePublicationChapter(
        string languageCode,
        string publicationCode,
        int bookNumber,
        int chapter)
    {
        var currentBook = await mediaService.GetBibleBook(languageCode, publicationCode, bookNumber) 
            ?? throw new InvalidOperationException($"Bible book not found: languageCode={languageCode}, publicationCode={publicationCode}, bookNumber={bookNumber}");
        
        var chapters = await mediaService.GetBiblePublicationChapters(languageCode, publicationCode, bookNumber);
        var previousChapter = chapters.Reverse().SkipWhile(kvp => kvp.Key >= chapter).FirstOrDefault();

        if (!previousChapter.Equals(default(KeyValuePair<int, BiblePublicationChapter>)))
        {
            return new KeyValuePair<BibleBook, BiblePublicationChapter>(currentBook, previousChapter.Value);
        }

        var previousBook = await GetPreviousBibleBook(languageCode, publicationCode, bookNumber);
        if (previousBook.Value == null)
        {
            throw new InvalidOperationException($"Previous bible book not found: languageCode={languageCode}, publicationCode={publicationCode}, bookNumber={bookNumber}");
        }

        chapters = await mediaService.GetBiblePublicationChapters(languageCode, publicationCode, previousBook.Key);
        if (chapters.Count == 0)
        {
            throw new InvalidOperationException($"No chapters in previous book: languageCode={languageCode}, publicationCode={publicationCode}, bookNumber={previousBook.Key}");
        }

        return new KeyValuePair<BibleBook, BiblePublicationChapter>(previousBook.Value, chapters.ElementAt(chapters.Count - 1).Value);
    }

    /// <summary>
    /// Gets the previous Bible book.
    /// </summary>
    public async Task<KeyValuePair<int, BibleBook>> GetPreviousBibleBook(
        string languageCode,
        string publicationCode,
        int bookNumber)
    {
        var books = await mediaService.GetBibleBooks(languageCode, publicationCode);
        if (books.Count == 0)
        {
            throw new InvalidOperationException($"No bible books found: languageCode={languageCode}, publicationCode={publicationCode}");
        }

        var previousBook = books.Reverse().SkipWhile(kvp => kvp.Key >= bookNumber).FirstOrDefault();

        if (!previousBook.Equals(default(KeyValuePair<int, BibleBook>)))
        {
            return previousBook;
        }

        var maxKey = books.Keys.Max();
        if (!books.TryGetValue(maxKey, out var maxBook))
        {
            throw new InvalidOperationException($"Bible book with key {maxKey} not found: languageCode={languageCode}, publicationCode={publicationCode}");
        }

        return new KeyValuePair<int, BibleBook>(maxKey, maxBook);
    }

    /// <summary>
    /// Gets the next Bible book.
    /// </summary>
    public async Task<KeyValuePair<int, BibleBook>> GetNextBibleBook(
        string languageCode,
        string publicationCode,
        int bookNumber)
    {
        var books = await mediaService.GetBibleBooks(languageCode, publicationCode);
        if (books.Count == 0)
        {
            throw new InvalidOperationException($"No bible books found: languageCode={languageCode}, publicationCode={publicationCode}");
        }

        var nextBook = books.SkipWhile(kvp => kvp.Key <= bookNumber).FirstOrDefault();

        if (!nextBook.Equals(default(KeyValuePair<int, BibleBook>)))
        {
            return nextBook;
        }

        var minKey = books.Keys.Min();
        if (!books.TryGetValue(minKey, out var minBook))
        {
            throw new InvalidOperationException($"Bible book with key {minKey} not found: languageCode={languageCode}, publicationCode={publicationCode}");
        }

        return new KeyValuePair<int, BibleBook>(minKey, minBook);
    }
}
