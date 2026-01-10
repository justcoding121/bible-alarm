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
/// Handles complex selection logic for sections, tracks, and translations.
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

    public async Task<(int SectionNumber, int TrackNumber, string SectionName)> GetSectionAndTrackForTranslationAsync(
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

        if (isSameTranslation && HasValidSectionAndTrack(currentSchedule))
        {
            return await GetPreservedSectionAndTrackAsync(currentSchedule!, publication, sections);
        }

        return await GetFirstSectionAndTrackAsync(language.Code, publication, sections);
    }

    public async Task<(string? PublicationCode, int SectionNumber, int TrackNumber, string SectionName, string PublicationName)>
        GetTranslationSectionAndTrackForLanguageAsync(LanguageListViewItemModel language)
    {
        var currentSchedule = state.Value.CurrentSchedule;
        var isSameLanguage = currentSchedule != null &&
                           currentSchedule.BiblePublicationLanguageCode == language.Code;

        var translations = await Task.Run(async () =>
            await mediaService.GetBiblePublications(language.Code));

        if (translations == null || translations.Count == 0)
        {
            return (null, 0, 0, string.Empty, string.Empty);
        }

        if (isSameLanguage && CanPreserveCurrentTranslation(currentSchedule!, translations))
        {
            return await GetPreservedTranslationSectionAndTrackAsync(currentSchedule!, language, translations);
        }

        return await GetDefaultTranslationSectionAndTrackAsync(language, translations);
    }

    private static bool IsSameTranslation(ScheduleStateItem? currentSchedule, PublicationListViewItemModel publication)
    {
        return currentSchedule != null &&
               currentSchedule.BiblePublicationLanguageCode == publication.Code &&
               currentSchedule.BiblePublicationPublicationCode == publication.Code;
    }

    private static bool HasValidSectionAndTrack(ScheduleStateItem? schedule)
    {
        return schedule != null &&
               schedule.BiblePublicationSectionNumber.HasValue &&
               schedule.BiblePublicationTrackNumber.HasValue;
    }

    private async Task<(int SectionNumber, int TrackNumber, string SectionName)> GetPreservedSectionAndTrackAsync(
        ScheduleStateItem currentSchedule,
        PublicationListViewItemModel publication,
        IDictionary<int, BibleSection> sections)
    {
        var currentSectionNumber = currentSchedule.BiblePublicationSectionNumber!.Value;
        var currentTrackNumber = currentSchedule.BiblePublicationTrackNumber!.Value;

        if (sections.TryGetValue(currentSectionNumber, out var currentSection))
        {
            var tracks = await Task.Run(async () =>
                await mediaService.GetBibleTracks(currentSchedule.BiblePublicationLanguageCode!, publication.Code, currentSectionNumber));

            var trackNumber = tracks != null && tracks.ContainsKey(currentTrackNumber)
                ? currentTrackNumber
                : tracks?.Values.First().Number ?? 1;

            return (currentSectionNumber, trackNumber, currentSection.Name);
        }

        // Use the language code from the schedule directly
        var languageCode = currentSchedule.BiblePublicationLanguageCode ?? "en";
        return await GetFirstSectionAndTrackAsync(languageCode, publication, sections);
    }

    private async Task<(int SectionNumber, int TrackNumber, string SectionName)> GetFirstSectionAndTrackAsync(
        string languageCode,
        PublicationListViewItemModel publication,
        IDictionary<int, BibleSection> sections)
    {
        var firstSection = sections.Values.First();
        var tracks = await Task.Run(async () =>
            await mediaService.GetBibleTracks(languageCode, publication.Code, firstSection.Number));

        if (tracks == null || tracks.Count == 0)
        {
            return (0, 0, string.Empty);
        }

        return (firstSection.Number, tracks.Values.First().Number, firstSection.Name);
    }

    private static bool CanPreserveCurrentTranslation(ScheduleStateItem schedule, Dictionary<string, BiblePublication> translations)
    {
        return !string.IsNullOrEmpty(schedule.BiblePublicationPublicationCode) &&
               translations.ContainsKey(schedule.BiblePublicationPublicationCode) &&
               schedule.BiblePublicationSectionNumber.HasValue &&
               schedule.BiblePublicationTrackNumber.HasValue;
    }

    private async Task<(string PublicationCode, int SectionNumber, int TrackNumber, string SectionName, string PublicationName)>
        GetPreservedTranslationSectionAndTrackAsync(
            ScheduleStateItem currentSchedule,
            LanguageListViewItemModel language,
            Dictionary<string, BiblePublication> translations)
    {
        var publicationCode = currentSchedule.BiblePublicationPublicationCode!;
        var currentSectionNumber = currentSchedule.BiblePublicationSectionNumber!.Value;
        var currentTrackNumber = currentSchedule.BiblePublicationTrackNumber!.Value;

        var sections = await Task.Run(async () =>
            await mediaService.GetBibleSections(language.Code, publicationCode));

        if (sections != null && sections.TryGetValue(currentSectionNumber, out var currentSection))
        {
            var tracks = await Task.Run(async () =>
                await mediaService.GetBibleTracks(language.Code, publicationCode, currentSectionNumber));

            var trackNumber = tracks != null && tracks.ContainsKey(currentTrackNumber)
                ? currentTrackNumber
                : tracks?.Values.First().Number ?? 1;

            var publicationName = translations.TryGetValue(publicationCode, out var pub) ? pub.Name : publicationCode;
            return (publicationCode, currentSectionNumber, trackNumber, currentSection.Name, publicationName);
        }

        return await GetFirstSectionForTranslationAsync(language, publicationCode, translations);
    }

    private async Task<(string PublicationCode, int SectionNumber, int TrackNumber, string SectionName, string PublicationName)>
        GetDefaultTranslationSectionAndTrackAsync(
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

    private async Task<(string PublicationCode, int SectionNumber, int TrackNumber, string SectionName, string PublicationName)>
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
        var tracks = await Task.Run(async () =>
            await mediaService.GetBibleTracks(language.Code, publicationCode, firstSection.Number));

        if (tracks == null || tracks.Count == 0)
        {
            return (string.Empty, 0, 0, string.Empty, string.Empty);
        }

        var publicationName = translations.TryGetValue(publicationCode, out var pub) ? pub.Name : publicationCode;
        return (publicationCode, firstSection.Number, tracks.Values.First().Number, firstSection.Name, publicationName);
    }
}
