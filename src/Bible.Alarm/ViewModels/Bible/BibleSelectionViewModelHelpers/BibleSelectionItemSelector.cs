#nullable enable
using Bible;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.Bible;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.Shared;
using Fluxor;

namespace Bible.Alarm.ViewModels.Bible.BibleSelectionViewModelHelpers;

/// <summary>
/// Handles complex selection logic for books, chapters, and translations.
/// </summary>
public sealed class BibleSelectionItemSelector
{
    private readonly IMediaService mediaService;
    private readonly IState<ApplicationState> state;

    public BibleSelectionItemSelector(
        IMediaService mediaService,
        IState<ApplicationState> state)
    {
        this.mediaService = mediaService;
        this.state = state;
    }

    public async Task<(int BookNumber, int ChapterNumber, string BookName)> GetBookAndChapterForTranslationAsync(
        PublicationListViewItemModel publication,
        LanguageListViewItemModel language)
    {
        var currentSchedule = state.Value.CurrentSchedule;
        var isSameTranslation = IsSameTranslation(currentSchedule, publication);

        var books = await Task.Run(async () =>
            await mediaService.GetBibleBooks(language.Code, publication.Code));

        if (books == null || books.Count == 0)
        {
            return (0, 0, string.Empty);
        }

        if (isSameTranslation && HasValidBookAndChapter(currentSchedule))
        {
            return await GetPreservedBookAndChapterAsync(currentSchedule!, publication, books);
        }

        return await GetFirstBookAndChapterAsync(language.Code, publication, books);
    }

    public async Task<(string? PublicationCode, int BookNumber, int ChapterNumber, string BookName, string PublicationName)>
        GetTranslationBookAndChapterForLanguageAsync(LanguageListViewItemModel language)
    {
        var currentSchedule = state.Value.CurrentSchedule;
        var isSameLanguage = currentSchedule != null &&
                           currentSchedule.BibleReadingLanguageCode == language.Code;

        var translations = await Task.Run(async () =>
            await mediaService.GetBibleTranslations(language.Code));

        if (translations == null || translations.Count == 0)
        {
            return (null, 0, 0, string.Empty, string.Empty);
        }

        if (isSameLanguage && CanPreserveCurrentTranslation(currentSchedule!, translations))
        {
            return await GetPreservedTranslationBookAndChapterAsync(currentSchedule!, language, translations);
        }

        return await GetDefaultTranslationBookAndChapterAsync(language, translations);
    }

    private static bool IsSameTranslation(ScheduleStateItem? currentSchedule, PublicationListViewItemModel publication)
    {
        return currentSchedule != null &&
               currentSchedule.BibleReadingLanguageCode == publication.Code &&
               currentSchedule.BibleReadingPublicationCode == publication.Code;
    }

    private static bool HasValidBookAndChapter(ScheduleStateItem? schedule)
    {
        return schedule != null &&
               schedule.BibleReadingBookNumber.HasValue &&
               schedule.BibleReadingChapterNumber.HasValue;
    }

    private async Task<(int BookNumber, int ChapterNumber, string BookName)> GetPreservedBookAndChapterAsync(
        ScheduleStateItem currentSchedule,
        PublicationListViewItemModel publication,
        IDictionary<int, BibleBook> books)
    {
        var currentBookNumber = currentSchedule.BibleReadingBookNumber!.Value;
        var currentChapterNumber = currentSchedule.BibleReadingChapterNumber!.Value;

        if (books.TryGetValue(currentBookNumber, out var currentBook))
        {
            var chapters = await Task.Run(async () =>
                await mediaService.GetBibleChapters(currentSchedule.BibleReadingLanguageCode!, publication.Code, currentBookNumber));

            var chapterNumber = chapters != null && chapters.ContainsKey(currentChapterNumber)
                ? currentChapterNumber
                : chapters?.Values.First().Number ?? 1;

            return (currentBookNumber, chapterNumber, currentBook.Name);
        }

        // Use the language code from the schedule directly
        var languageCode = currentSchedule.BibleReadingLanguageCode ?? "en";
        return await GetFirstBookAndChapterAsync(languageCode, publication, books);
    }

    private async Task<(int BookNumber, int ChapterNumber, string BookName)> GetFirstBookAndChapterAsync(
        string languageCode,
        PublicationListViewItemModel publication,
        IDictionary<int, BibleBook> books)
    {
        var firstBook = books.Values.First();
        var chapters = await Task.Run(async () =>
            await mediaService.GetBibleChapters(languageCode, publication.Code, firstBook.Number));

        if (chapters == null || chapters.Count == 0)
        {
            return (0, 0, string.Empty);
        }

        return (firstBook.Number, chapters.Values.First().Number, firstBook.Name);
    }

    private static bool CanPreserveCurrentTranslation(ScheduleStateItem schedule, Dictionary<string, BibleTranslation> translations)
    {
        return !string.IsNullOrEmpty(schedule.BibleReadingPublicationCode) &&
               translations.ContainsKey(schedule.BibleReadingPublicationCode) &&
               schedule.BibleReadingBookNumber.HasValue &&
               schedule.BibleReadingChapterNumber.HasValue;
    }

    private async Task<(string PublicationCode, int BookNumber, int ChapterNumber, string BookName, string PublicationName)>
        GetPreservedTranslationBookAndChapterAsync(
            ScheduleStateItem currentSchedule,
            LanguageListViewItemModel language,
            Dictionary<string, BibleTranslation> translations)
    {
        var publicationCode = currentSchedule.BibleReadingPublicationCode!;
        var currentBookNumber = currentSchedule.BibleReadingBookNumber!.Value;
        var currentChapterNumber = currentSchedule.BibleReadingChapterNumber!.Value;

        var books = await Task.Run(async () =>
            await mediaService.GetBibleBooks(language.Code, publicationCode));

        if (books != null && books.TryGetValue(currentBookNumber, out var currentBook))
        {
            var chapters = await Task.Run(async () =>
                await mediaService.GetBibleChapters(language.Code, publicationCode, currentBookNumber));

            var chapterNumber = chapters != null && chapters.ContainsKey(currentChapterNumber)
                ? currentChapterNumber
                : chapters?.Values.First().Number ?? 1;

            var publicationName = translations.TryGetValue(publicationCode, out var pub) ? pub.Name : publicationCode;
            return (publicationCode, currentBookNumber, chapterNumber, currentBook.Name, publicationName);
        }

        return await GetFirstBookForTranslationAsync(language, publicationCode, translations);
    }

    private async Task<(string PublicationCode, int BookNumber, int ChapterNumber, string BookName, string PublicationName)>
        GetDefaultTranslationBookAndChapterAsync(
            LanguageListViewItemModel language,
            Dictionary<string, BibleTranslation> translations)
    {
        var lastTranslation = translations.LastOrDefault();
        if (lastTranslation.Value == null)
        {
            return (string.Empty, 0, 0, string.Empty, string.Empty);
        }

        var publicationCode = lastTranslation.Key;
        return await GetFirstBookForTranslationAsync(language, publicationCode, translations);
    }

    private async Task<(string PublicationCode, int BookNumber, int ChapterNumber, string BookName, string PublicationName)>
        GetFirstBookForTranslationAsync(
            LanguageListViewItemModel language,
            string publicationCode,
            Dictionary<string, BibleTranslation> translations)
    {
        var books = await Task.Run(async () =>
            await mediaService.GetBibleBooks(language.Code, publicationCode));

        if (books == null || books.Count == 0)
        {
            return (string.Empty, 0, 0, string.Empty, string.Empty);
        }

        var firstBook = books.Values.First();
        var chapters = await Task.Run(async () =>
            await mediaService.GetBibleChapters(language.Code, publicationCode, firstBook.Number));

        if (chapters == null || chapters.Count == 0)
        {
            return (string.Empty, 0, 0, string.Empty, string.Empty);
        }

        var publicationName = translations.TryGetValue(publicationCode, out var pub) ? pub.Name : publicationCode;
        return (publicationCode, firstBook.Number, chapters.Values.First().Number, firstBook.Name, publicationName);
    }
}
