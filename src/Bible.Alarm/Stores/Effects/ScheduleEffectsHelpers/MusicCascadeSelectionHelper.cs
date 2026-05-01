#nullable enable

using System.Linq;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Stores.Effects.ScheduleEffectsHelpers;

internal static class MusicCascadeSelectionHelper
{
    internal static Task<(string? SectionCode, string SectionName, string TrackCode, string TrackTitle)> GetFirstSectionAndTrackAsync(
        IMediaService mediaService,
        string languageCode,
        string publicationCode,
        bool publicationWithoutLanguage) =>
        publicationWithoutLanguage
            ? GetFirstSectionAndTrackWithoutLanguageAsync(mediaService, publicationCode)
            : GetFirstSectionAndTrackWithLanguageAsync(mediaService, languageCode, publicationCode);

    private static async Task<(string? SectionCode, string SectionName, string TrackCode, string TrackTitle)> GetFirstSectionAndTrackWithoutLanguageAsync(
        IMediaService mediaService,
        string publicationCode)
    {
        var sections = await mediaService.GetSectionsForPublicationWithoutLanguage(publicationCode);

        if (sections != null && sections.Count > 0)
        {
            using var sectionEnumerator = sections.GetEnumerator();
            _ = sectionEnumerator.MoveNext();
            var firstSection = sectionEnumerator.Current;
            var sectionCode = firstSection.Value.SectionCode;
            var sectionName = firstSection.Value.Name;

            var tracks = await mediaService.GetBiblePublicationTracks(string.Empty, publicationCode, sectionCode);
            if (tracks != null && tracks.Count > 0)
            {
                var firstTrack = tracks.Values.OrderBy(t => t, Comparer<BiblePublicationTrack>.Create((a, b) => a.CompareTo(b))).ToList()[0];
                return (sectionCode, sectionName, TrackCodeHelper.GetFromTrack(firstTrack), firstTrack.Title ?? string.Empty);
            }

            return (sectionCode, sectionName, string.Empty, string.Empty);
        }

        var flatTracks = await mediaService.GetBiblePublicationTracks(string.Empty, publicationCode, null);
        if (flatTracks != null && flatTracks.Count > 0)
        {
            var firstTrack = flatTracks.Values.OrderBy(t => t, Comparer<BiblePublicationTrack>.Create((a, b) => a.CompareTo(b))).ToList()[0];
            return (null, string.Empty, TrackCodeHelper.GetFromTrack(firstTrack), firstTrack.Title ?? string.Empty);
        }

        return (null, string.Empty, string.Empty, string.Empty);
    }

    private static async Task<(string? SectionCode, string SectionName, string TrackCode, string TrackTitle)> GetFirstSectionAndTrackWithLanguageAsync(
        IMediaService mediaService,
        string languageCode,
        string publicationCode)
    {
        var sectionsWithLanguage = await mediaService.GetBiblePublicationSections(languageCode, publicationCode);

        if (sectionsWithLanguage != null && sectionsWithLanguage.Count > 0)
        {
            using var sectionEnumerator = sectionsWithLanguage.GetEnumerator();
            _ = sectionEnumerator.MoveNext();
            var firstSection = sectionEnumerator.Current;
            var sectionCode = firstSection.Value.SectionCode;
            var sectionName = firstSection.Value.Name;

            var tracks = await mediaService.GetBiblePublicationTracks(languageCode, publicationCode, sectionCode);
            if (tracks != null && tracks.Count > 0)
            {
                var firstTrack = tracks.Values.OrderBy(t => t, Comparer<BiblePublicationTrack>.Create((a, b) => a.CompareTo(b))).ToList()[0];
                return (sectionCode, sectionName, TrackCodeHelper.GetFromTrack(firstTrack), firstTrack.Title ?? string.Empty);
            }

            return (sectionCode, sectionName, string.Empty, string.Empty);
        }

        var flatTracksWithLanguage = await mediaService.GetBiblePublicationTracks(languageCode, publicationCode, null);
        if (flatTracksWithLanguage != null && flatTracksWithLanguage.Count > 0)
        {
            var firstTrack = flatTracksWithLanguage.Values.OrderBy(t => t, Comparer<BiblePublicationTrack>.Create((a, b) => a.CompareTo(b))).ToList()[0];
            return (null, string.Empty, TrackCodeHelper.GetFromTrack(firstTrack), firstTrack.Title ?? string.Empty);
        }

        return (null, string.Empty, string.Empty, string.Empty);
    }
}

