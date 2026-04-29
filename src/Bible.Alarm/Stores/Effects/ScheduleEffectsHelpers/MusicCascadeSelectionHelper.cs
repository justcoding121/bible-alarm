#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Stores.Effects.ScheduleEffectsHelpers;

internal static class MusicCascadeSelectionHelper
{
    internal static async Task<(string? SectionCode, string SectionName, string TrackCode, string TrackTitle)> GetFirstSectionAndTrackAsync(
        IMediaService mediaService,
        string languageCode,
        string publicationCode,
        bool publicationWithoutLanguage)
    {
        if (publicationWithoutLanguage)
        {
            // For publications without language, check if it's sectioned
            var sections = await mediaService.GetSectionsForPublicationWithoutLanguage(publicationCode);

            if (sections != null && sections.Count > 0)
            {
                // Sectioned publication - get first section and track
                using var sectionEnumerator = sections.GetEnumerator();
                _ = sectionEnumerator.MoveNext();
                var firstSection = sectionEnumerator.Current;
                var sectionCode = firstSection.Value.SectionCode;
                var sectionName = firstSection.Value.Name;

                // Use GetBiblePublicationTracks with empty language code for publications without language
                var tracks = await mediaService.GetBiblePublicationTracks(string.Empty, publicationCode, sectionCode);
                if (tracks != null && tracks.Count > 0)
                {
                    var firstTrack = tracks.Values.OrderBy(t => t, Comparer<BiblePublicationTrack>.Create((a, b) => a.CompareTo(b))).ToList()[0];
                    return (sectionCode, sectionName, TrackCodeHelper.GetFromTrack(firstTrack), firstTrack.Title ?? string.Empty);
                }

                return (sectionCode, sectionName, string.Empty, string.Empty);
            }

            // Flat publication - get tracks directly
            var flatTracks = await mediaService.GetBiblePublicationTracks(string.Empty, publicationCode, null);
            if (flatTracks != null && flatTracks.Count > 0)
            {
                var firstTrack = flatTracks.Values.OrderBy(t => t, Comparer<BiblePublicationTrack>.Create((a, b) => a.CompareTo(b))).ToList()[0];
                return (null, string.Empty, TrackCodeHelper.GetFromTrack(firstTrack), firstTrack.Title ?? string.Empty);
            }

            return (null, string.Empty, string.Empty, string.Empty);
        }

        // For publications with language, use same logic as Bible publication cascade
        var sectionsWithLanguage = await mediaService.GetBiblePublicationSections(languageCode, publicationCode);

        if (sectionsWithLanguage != null && sectionsWithLanguage.Count > 0)
        {
            // Sectioned publication - get first section and track
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

        // Flat publication - get tracks directly
        var flatTracksWithLanguage = await mediaService.GetBiblePublicationTracks(languageCode, publicationCode, null);
        if (flatTracksWithLanguage != null && flatTracksWithLanguage.Count > 0)
        {
            var firstTrack = flatTracksWithLanguage.Values.OrderBy(t => t, Comparer<BiblePublicationTrack>.Create((a, b) => a.CompareTo(b))).ToList()[0];
            return (null, string.Empty, TrackCodeHelper.GetFromTrack(firstTrack), firstTrack.Title ?? string.Empty);
        }

        return (null, string.Empty, string.Empty, string.Empty);
    }
}

