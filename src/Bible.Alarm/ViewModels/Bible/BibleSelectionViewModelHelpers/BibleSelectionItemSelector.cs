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
/// Handles complex selection logic for sections, chapters, and translations.
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

    public async Task<(int SectionNumber, int ChapterNumber, string SectionName)> GetSectionAndChapterForTranslationAsync(
        PublicationListViewItemModel publication,
        LanguageListViewItemModel language)
    {
        var currentSchedule = state.Value.CurrentSchedule;
        var isSameTranslation = IsSameTranslation(currentSchedule, publication);

        var sections = await Task.Run(async () =>
            await mediaService.GetBibleSections(language.Code, publication.Code));

        if (sections == null || sections.Count == 0)
        {
            return (0, 0, string.Empty);
        }

        if (isSameTranslation && HasValidSectionAndChapter(currentSchedule))
        {
            return await GetPreservedSectionAndChapterAsync(currentSchedule!, publication, sections);
        }

        return await GetFirstSectionAndChapterAsync(language.Code, publication, sections);
    }

    public async Task<(string? PublicationCode, int SectionNumber, int ChapterNumber, string SectionName, string PublicationName)>
        GetTranslationSectionAndChapterForLanguageAsync(LanguageListViewItemModel language)
    {
        var currentSchedule = state.Value.CurrentSchedule;
        var isSameLanguage = currentSchedule != null &&
                           currentSchedule.BibleReadingLanguageCode == language.Code;

        var translations = await Task.Run(async () =>
            await mediaService.GetBiblePublications(language.Code));

        if (translations == null || translations.Count == 0)
        {
            return (null, 0, 0, string.Empty, string.Empty);
        }

        if (isSameLanguage && CanPreserveCurrentTranslation(currentSchedule!, translations))
        {
            return await GetPreservedTranslationSectionAndChapterAsync(currentSchedule!, language, translations);
        }

        return await GetDefaultTranslationSectionAndChapterAsync(language, translations);
    }

    private static bool IsSameTranslation(ScheduleStateItem? currentSchedule, PublicationListViewItemModel publication)
    {
        return currentSchedule != null &&
               currentSchedule.BibleReadingLanguageCode == publication.Code &&
               currentSchedule.BibleReadingPublicationCode == publication.Code;
    }

    private static bool HasValidSectionAndChapter(ScheduleStateItem? schedule)
    {
        return schedule != null &&
               schedule.BibleReadingSectionNumber.HasValue &&
               schedule.BibleReadingChapterNumber.HasValue;
    }

    private async Task<(int SectionNumber, int ChapterNumber, string SectionName)> GetPreservedSectionAndChapterAsync(
        ScheduleStateItem currentSchedule,
        PublicationListViewItemModel publication,
        IDictionary<int, BibleSection> sections)
    {
        var currentSectionNumber = currentSchedule.BibleReadingSectionNumber!.Value;
        var currentChapterNumber = currentSchedule.BibleReadingChapterNumber!.Value;

        if (sections.TryGetValue(currentSectionNumber, out var currentSection))
        {
            var chapters = await Task.Run(async () =>
                await mediaService.GetBibleChapters(currentSchedule.BibleReadingLanguageCode!, publication.Code, currentSectionNumber));

            var chapterNumber = chapters != null && chapters.ContainsKey(currentChapterNumber)
                ? currentChapterNumber
                : chapters?.Values.First().Number ?? 1;

            return (currentSectionNumber, chapterNumber, currentSection.Name);
        }

        // Use the language code from the schedule directly
        var languageCode = currentSchedule.BibleReadingLanguageCode ?? "en";
        return await GetFirstSectionAndChapterAsync(languageCode, publication, sections);
    }

    private async Task<(int SectionNumber, int ChapterNumber, string SectionName)> GetFirstSectionAndChapterAsync(
        string languageCode,
        PublicationListViewItemModel publication,
        IDictionary<int, BibleSection> sections)
    {
        var firstSection = sections.Values.First();
        var chapters = await Task.Run(async () =>
            await mediaService.GetBibleChapters(languageCode, publication.Code, firstSection.Number));

        if (chapters == null || chapters.Count == 0)
        {
            return (0, 0, string.Empty);
        }

        return (firstSection.Number, chapters.Values.First().Number, firstSection.Name);
    }

    private static bool CanPreserveCurrentTranslation(ScheduleStateItem schedule, Dictionary<string, BiblePublication> translations)
    {
        return !string.IsNullOrEmpty(schedule.BibleReadingPublicationCode) &&
               translations.ContainsKey(schedule.BibleReadingPublicationCode) &&
               schedule.BibleReadingSectionNumber.HasValue &&
               schedule.BibleReadingChapterNumber.HasValue;
    }

    private async Task<(string PublicationCode, int SectionNumber, int ChapterNumber, string SectionName, string PublicationName)>
        GetPreservedTranslationSectionAndChapterAsync(
            ScheduleStateItem currentSchedule,
            LanguageListViewItemModel language,
            Dictionary<string, BiblePublication> translations)
    {
        var publicationCode = currentSchedule.BibleReadingPublicationCode!;
        var currentSectionNumber = currentSchedule.BibleReadingSectionNumber!.Value;
        var currentChapterNumber = currentSchedule.BibleReadingChapterNumber!.Value;

        var sections = await Task.Run(async () =>
            await mediaService.GetBibleSections(language.Code, publicationCode));

        if (sections != null && sections.TryGetValue(currentSectionNumber, out var currentSection))
        {
            var chapters = await Task.Run(async () =>
                await mediaService.GetBibleChapters(language.Code, publicationCode, currentSectionNumber));

            var chapterNumber = chapters != null && chapters.ContainsKey(currentChapterNumber)
                ? currentChapterNumber
                : chapters?.Values.First().Number ?? 1;

            var publicationName = translations.TryGetValue(publicationCode, out var pub) ? pub.Name : publicationCode;
            return (publicationCode, currentSectionNumber, chapterNumber, currentSection.Name, publicationName);
        }

        return await GetFirstSectionForTranslationAsync(language, publicationCode, translations);
    }

    private async Task<(string PublicationCode, int SectionNumber, int ChapterNumber, string SectionName, string PublicationName)>
        GetDefaultTranslationSectionAndChapterAsync(
            LanguageListViewItemModel language,
            Dictionary<string, BiblePublication> translations)
    {
        var lastTranslation = translations.LastOrDefault();
        if (lastTranslation.Value == null)
        {
            return (string.Empty, 0, 0, string.Empty, string.Empty);
        }

        var publicationCode = lastTranslation.Key;
        return await GetFirstSectionForTranslationAsync(language, publicationCode, translations);
    }

    private async Task<(string PublicationCode, int SectionNumber, int ChapterNumber, string SectionName, string PublicationName)>
        GetFirstSectionForTranslationAsync(
            LanguageListViewItemModel language,
            string publicationCode,
            Dictionary<string, BiblePublication> translations)
    {
        var sections = await Task.Run(async () =>
            await mediaService.GetBibleSections(language.Code, publicationCode));

        if (sections == null || sections.Count == 0)
        {
            return (string.Empty, 0, 0, string.Empty, string.Empty);
        }

        var firstSection = sections.Values.First();
        var chapters = await Task.Run(async () =>
            await mediaService.GetBibleChapters(language.Code, publicationCode, firstSection.Number));

        if (chapters == null || chapters.Count == 0)
        {
            return (string.Empty, 0, 0, string.Empty, string.Empty);
        }

        var publicationName = translations.TryGetValue(publicationCode, out var pub) ? pub.Name : publicationCode;
        return (publicationCode, firstSection.Number, chapters.Values.First().Number, firstSection.Name, publicationName);
    }
}
