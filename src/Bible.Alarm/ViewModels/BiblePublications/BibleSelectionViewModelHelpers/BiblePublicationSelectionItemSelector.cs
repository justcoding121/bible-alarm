#nullable enable
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.Shared;
using Fluxor;

namespace Bible.Alarm.ViewModels.BiblePublications.BibleSelectionViewModelHelpers;

/// <summary>
/// Handles complex selection logic for sections, tracks, and publications.
/// </summary>
public sealed class BiblePublicationSelectionItemSelector
{
    private readonly IMediaService mediaService;
    private readonly IBiblePublicationService? biblePublicationService;
    private readonly IState<ApplicationState> state;

    public BiblePublicationSelectionItemSelector(
        IMediaService mediaService,
        IState<ApplicationState> state,
        IBiblePublicationService? biblePublicationService = null)
    {
        this.mediaService = mediaService;
        this.state = state;
        this.biblePublicationService = biblePublicationService;
    }

    public async Task<(int SectionNumber, int TrackNumber, string SectionName, string TrackTitle)> GetSectionAndTrackForPublicationAsync(
        PublicationListViewItemModel publication,
        LanguageListViewItemModel language)
    {
        var currentSchedule = state.Value.CurrentSchedule;
        var isSamePublication = IsSamePublication(currentSchedule, publication);

        var sections = await Task.Run(async () =>
            await mediaService.GetBiblePublicationSections(language.Code, publication.Code));

        if (sections == null || sections.Count == 0)
        {
            return (0, 0, string.Empty, string.Empty);
        }

        if (isSamePublication && HasValidSectionAndTrack(currentSchedule))
        {
            return await GetPreservedSectionAndTrackAsync(currentSchedule!, publication, sections);
        }

        return await GetFirstSectionAndTrackAsync(language.Code, publication, sections);
    }

    public async Task<(string? PublicationCode, int SectionNumber, int TrackNumber, string SectionName, string PublicationName, string TrackTitle)>
        GetPublicationSectionAndTrackForLanguageAsync(LanguageListViewItemModel language)
    {
        var currentSchedule = state.Value.CurrentSchedule;
        var isSameLanguage = currentSchedule != null &&
                           currentSchedule.BiblePublicationLanguageCode == language.Code;

        var publications = await Task.Run(async () =>
            await mediaService.GetBiblePublications(language.Code));

        if (publications == null || publications.Count == 0)
        {
            return (null, 0, 0, string.Empty, string.Empty, string.Empty);
        }

        if (isSameLanguage && CanPreserveCurrentPublication(currentSchedule!, publications))
        {
            return await GetPreservedPublicationSectionAndTrackAsync(currentSchedule!, language, publications);
        }

        return await GetDefaultPublicationSectionAndTrackAsync(language, publications);
    }

    private static bool IsSamePublication(ScheduleStateItem? currentSchedule, PublicationListViewItemModel publication)
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

    private static bool CanPreserveCurrentPublication(ScheduleStateItem schedule, Dictionary<string, BiblePublication> publications)
    {
        return !string.IsNullOrEmpty(schedule.BiblePublicationCode) &&
               publications.ContainsKey(schedule.BiblePublicationCode) &&
               schedule.BiblePublicationSectionNumber.HasValue &&
               schedule.BiblePublicationTrackNumber.HasValue;
    }

    private async Task<(string PublicationCode, int SectionNumber, int TrackNumber, string SectionName, string PublicationName, string TrackTitle)>
        GetPreservedPublicationSectionAndTrackAsync(
            ScheduleStateItem currentSchedule,
            LanguageListViewItemModel language,
            Dictionary<string, BiblePublication> publications)
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
                var publicationName = publications.TryGetValue(publicationCode, out var pub) ? pub.Name : publicationCode;
                return (publicationCode, currentSectionNumber, currentTrackNumber, currentSection.Name, publicationName, track.Title);
            }

            var firstTrack = tracks?.Values.FirstOrDefault();
            var trackNumber = firstTrack?.Number ?? 1;
            var trackTitle = firstTrack?.Title ?? string.Empty;

            var pubName = publications.TryGetValue(publicationCode, out var p) ? p.Name : publicationCode;
            return (publicationCode, currentSectionNumber, trackNumber, currentSection.Name, pubName, trackTitle);
        }

        return await GetFirstSectionForPublicationAsync(language, publicationCode, publications);
    }

    private async Task<(string PublicationCode, int SectionNumber, int TrackNumber, string SectionName, string PublicationName, string TrackTitle)>
        GetDefaultPublicationSectionAndTrackAsync(
            LanguageListViewItemModel language,
            Dictionary<string, BiblePublication> publications)
    {
        var lastPublication = publications.LastOrDefault();
        if (lastPublication.Value == null)
        {
            return (string.Empty, 0, 0, string.Empty, string.Empty, string.Empty);
        }

        var publicationCode = lastPublication.Key;
        return await GetFirstSectionForPublicationAsync(language, publicationCode, publications);
    }

    private async Task<(string PublicationCode, int SectionNumber, int TrackNumber, string SectionName, string PublicationName, string TrackTitle)>
        GetFirstSectionForPublicationAsync(
            LanguageListViewItemModel language,
            string publicationCode,
            Dictionary<string, BiblePublication> publications)
    {
        var publicationName = publications.TryGetValue(publicationCode, out var pub) ? pub.Name : publicationCode;

        // Check if this is a non-sectioned publication (drama/video)
        if (!PublicationTypeHelper.HasSectionStructure(publicationCode))
        {
            return await GetFirstTrackForNonSectionedPublicationAsync(language, publicationCode, publicationName);
        }

        // Sectioned publications (traditional Bible)
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
        return (publicationCode, firstSection.Number, firstTrack.Number, firstSection.Name, publicationName, firstTrack.Title);
    }

    /// <summary>
    /// Gets the first track for a non-sectioned publication (drama/video).
    /// Non-sectioned publications have tracks directly on the publication without sections.
    /// </summary>
    private async Task<(string PublicationCode, int SectionNumber, int TrackNumber, string SectionName, string PublicationName, string TrackTitle)>
        GetFirstTrackForNonSectionedPublicationAsync(
            LanguageListViewItemModel language,
            string publicationCode,
            string publicationName)
    {
        if (biblePublicationService == null)
        {
            return (string.Empty, 0, 0, string.Empty, string.Empty, string.Empty);
        }

        var publication = await Task.Run(async () =>
            await biblePublicationService.GetByLanguageAndCodeWithTracksAsync(language.Code, publicationCode));

        if (publication == null || publication.Tracks == null || publication.Tracks.Count == 0)
        {
            return (string.Empty, 0, 0, string.Empty, string.Empty, string.Empty);
        }

        var firstTrack = publication.Tracks.OrderBy(t => t.Number).First();
        // For non-sectioned publications, SectionNumber is not applicable (use 0 or null)
        // SectionName is empty since there's no section
        return (publicationCode, 0, firstTrack.Number, string.Empty, publicationName, firstTrack.Title);
    }
}
