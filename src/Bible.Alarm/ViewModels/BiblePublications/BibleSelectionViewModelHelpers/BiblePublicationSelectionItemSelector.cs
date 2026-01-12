#nullable enable
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.Shared;
using Fluxor;

namespace Bible.Alarm.ViewModels.BiblePublications.BibleSelectionViewModelHelpers;

/// <summary>
/// Handles complex selection logic for sections, tracks, and translations.
/// </summary>
public sealed class BiblePublicationSelectionItemSelector
{
    private readonly IMediaService mediaService;
    private readonly IState<ApplicationState> state;

    public BiblePublicationSelectionItemSelector(
        IMediaService mediaService,
        IState<ApplicationState> state)
    {
        this.mediaService = mediaService;
        this.state = state;
    }

    public async Task<(int SectionNumber, int TrackNumber, string SectionName, string TrackTitle)> GetSectionAndTrackForTranslationAsync(
        PublicationListViewItemModel publication,
        LanguageListViewItemModel language)
    {
        var currentSchedule = state.Value.CurrentSchedule;
        var isSameTranslation = IsSameTranslation(currentSchedule, publication);

        var sections = await Task.Run(async () =>
            await mediaService.GetBiblePublicationSections(language.Code, publication.Code));

        if (sections == null || sections.Count == 0)
        {
            return (0, 0, string.Empty, string.Empty);
        }

        if (isSameTranslation && HasValidSectionAndTrack(currentSchedule))
        {
            return await GetPreservedSectionAndTrackAsync(currentSchedule!, publication, sections);
        }

        return await GetFirstSectionAndTrackAsync(language.Code, publication, sections);
    }

    public async Task<(string? PublicationCode, int SectionNumber, int TrackNumber, string SectionName, string PublicationName, string TrackTitle)>
        GetTranslationSectionAndTrackForLanguageAsync(LanguageListViewItemModel language)
    {
        var currentSchedule = state.Value.CurrentSchedule;
        var isSameLanguage = currentSchedule != null &&
                           currentSchedule.BiblePublicationLanguageCode == language.Code;

        var translations = await Task.Run(async () =>
            await mediaService.GetBiblePublications(language.Code));

        if (translations == null || translations.Count == 0)
        {
            return (null, 0, 0, string.Empty, string.Empty, string.Empty);
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
               currentSchedule.BiblePublicationCode == publication.Code;
    }

    private static bool HasValidSectionAndTrack(ScheduleStateItem? schedule)
    {
        return schedule != null &&
               schedule.BiblePublicationSectionNumber.HasValue &&
               schedule.BiblePublicationTrackNumber.HasValue;
    }

    private async Task<(int SectionNumber, int TrackNumber, string SectionName, string TrackTitle)> GetPreservedSectionAndTrackAsync(
        ScheduleStateItem currentSchedule,
        PublicationListViewItemModel publication,
        IDictionary<int, BiblePublicationSection> sections)
    {
        var currentSectionNumber = currentSchedule.BiblePublicationSectionNumber!.Value;
        var currentTrackNumber = currentSchedule.BiblePublicationTrackNumber!.Value;

        if (sections.TryGetValue(currentSectionNumber, out var currentSection))
        {
            var tracks = await Task.Run(async () =>
                await mediaService.GetBiblePublicationTracks(currentSchedule.BiblePublicationLanguageCode!, publication.Code, currentSectionNumber));

            if (tracks != null && tracks.TryGetValue(currentTrackNumber, out var track))
            {
                return (currentSectionNumber, currentTrackNumber, currentSection.Name, track.Title);
            }

            var firstTrack = tracks?.Values.FirstOrDefault();
            var trackNumber = firstTrack?.Number ?? 1;
            var trackTitle = firstTrack?.Title ?? string.Empty;

            return (currentSectionNumber, trackNumber, currentSection.Name, trackTitle);
        }

        // Use the language code from the schedule directly
        var languageCode = currentSchedule.BiblePublicationLanguageCode ?? "en";
        return await GetFirstSectionAndTrackAsync(languageCode, publication, sections);
    }

    private async Task<(int SectionNumber, int TrackNumber, string SectionName, string TrackTitle)> GetFirstSectionAndTrackAsync(
        string languageCode,
        PublicationListViewItemModel publication,
        IDictionary<int, BiblePublicationSection> sections)
    {
        var firstSection = sections.Values.First();
        var tracks = await Task.Run(async () =>
            await mediaService.GetBiblePublicationTracks(languageCode, publication.Code, firstSection.Number));

        if (tracks == null || tracks.Count == 0)
        {
            return (0, 0, string.Empty, string.Empty);
        }

        var firstTrack = tracks.Values.First();
        return (firstSection.Number, firstTrack.Number, firstSection.Name, firstTrack.Title);
    }

    private static bool CanPreserveCurrentTranslation(ScheduleStateItem schedule, Dictionary<string, BiblePublication> translations)
    {
        return !string.IsNullOrEmpty(schedule.BiblePublicationCode) &&
               translations.ContainsKey(schedule.BiblePublicationCode) &&
               schedule.BiblePublicationSectionNumber.HasValue &&
               schedule.BiblePublicationTrackNumber.HasValue;
    }

    private async Task<(string PublicationCode, int SectionNumber, int TrackNumber, string SectionName, string PublicationName, string TrackTitle)>
        GetPreservedTranslationSectionAndTrackAsync(
            ScheduleStateItem currentSchedule,
            LanguageListViewItemModel language,
            Dictionary<string, BiblePublication> translations)
    {
        var publicationCode = currentSchedule.BiblePublicationCode!;
        var currentSectionNumber = currentSchedule.BiblePublicationSectionNumber!.Value;
        var currentTrackNumber = currentSchedule.BiblePublicationTrackNumber!.Value;

        var sections = await Task.Run(async () =>
            await mediaService.GetBiblePublicationSections(language.Code, publicationCode));

        if (sections != null && sections.TryGetValue(currentSectionNumber, out var currentSection))
        {
            var tracks = await Task.Run(async () =>
                await mediaService.GetBiblePublicationTracks(language.Code, publicationCode, currentSectionNumber));

            if (tracks != null && tracks.TryGetValue(currentTrackNumber, out var track))
            {
                var publicationName = translations.TryGetValue(publicationCode, out var pub) ? pub.Name : publicationCode;
                return (publicationCode, currentSectionNumber, currentTrackNumber, currentSection.Name, publicationName, track.Title);
            }

            var firstTrack = tracks?.Values.FirstOrDefault();
            var trackNumber = firstTrack?.Number ?? 1;
            var trackTitle = firstTrack?.Title ?? string.Empty;

            var pubName = translations.TryGetValue(publicationCode, out var p) ? p.Name : publicationCode;
            return (publicationCode, currentSectionNumber, trackNumber, currentSection.Name, pubName, trackTitle);
        }

        return await GetFirstSectionForTranslationAsync(language, publicationCode, translations);
    }

    private async Task<(string PublicationCode, int SectionNumber, int TrackNumber, string SectionName, string PublicationName, string TrackTitle)>
        GetDefaultTranslationSectionAndTrackAsync(
            LanguageListViewItemModel language,
            Dictionary<string, BiblePublication> translations)
    {
        var lastTranslation = translations.LastOrDefault();
        if (lastTranslation.Value == null)
        {
            return (string.Empty, 0, 0, string.Empty, string.Empty, string.Empty);
        }

        var publicationCode = lastTranslation.Key;
        return await GetFirstSectionForTranslationAsync(language, publicationCode, translations);
    }

    private async Task<(string PublicationCode, int SectionNumber, int TrackNumber, string SectionName, string PublicationName, string TrackTitle)>
        GetFirstSectionForTranslationAsync(
            LanguageListViewItemModel language,
            string publicationCode,
            Dictionary<string, BiblePublication> translations)
    {
        var sections = await Task.Run(async () =>
            await mediaService.GetBiblePublicationSections(language.Code, publicationCode));

        if (sections == null || sections.Count == 0)
        {
            return (string.Empty, 0, 0, string.Empty, string.Empty, string.Empty);
        }

        var firstSection = sections.Values.First();
        var tracks = await Task.Run(async () =>
            await mediaService.GetBiblePublicationTracks(language.Code, publicationCode, firstSection.Number));

        if (tracks == null || tracks.Count == 0)
        {
            return (string.Empty, 0, 0, string.Empty, string.Empty, string.Empty);
        }

        var firstTrack = tracks.Values.First();
        var publicationName = translations.TryGetValue(publicationCode, out var pub) ? pub.Name : publicationCode;
        return (publicationCode, firstSection.Number, firstTrack.Number, firstSection.Name, publicationName, firstTrack.Title);
    }
}
