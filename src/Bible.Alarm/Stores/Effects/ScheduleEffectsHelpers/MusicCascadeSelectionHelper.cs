#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Stores.Effects.ScheduleEffectsHelpers;

internal static class MusicCascadeSelectionHelper
{
    internal static async Task<(string? SectionCode, string SectionName, int TrackNumber, string TrackTitle)> GetFirstSectionAndTrackAsync(
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
                var firstSection = sections.First();
                var sectionCode = firstSection.Value.SectionCode;
                var sectionNum = firstSection.Key;
                var sectionName = firstSection.Value.Name;

                // Use GetBiblePublicationTracks with empty language code for publications without language
                var tracks = await mediaService.GetBiblePublicationTracks(string.Empty, publicationCode, sectionNum);
                if (tracks != null && tracks.Count > 0)
                {
                    var firstTrack = tracks.Values.OrderBy(t => t.Number).First();
                    return (sectionCode, sectionName, firstTrack.Number, firstTrack.Title ?? string.Empty);
                }

                return (sectionCode, sectionName, 0, string.Empty);
            }

            // Flat publication - get tracks directly (sectionNumber = 0)
            var flatTracks = await mediaService.GetBiblePublicationTracks(string.Empty, publicationCode, 0);
            if (flatTracks != null && flatTracks.Count > 0)
            {
                var firstTrack = flatTracks.Values.OrderBy(t => t.Number).First();
                return (null, string.Empty, firstTrack.Number, firstTrack.Title ?? string.Empty);
            }

            return (null, string.Empty, 0, string.Empty);
        }

        // For publications with language, use same logic as Bible publication cascade
        var sectionsWithLanguage = await mediaService.GetBiblePublicationSections(languageCode, publicationCode);

        if (sectionsWithLanguage != null && sectionsWithLanguage.Count > 0)
        {
            // Sectioned publication - get first section and track
            var firstSection = sectionsWithLanguage.First();
            var sectionCode = firstSection.Value.SectionCode;
            var sectionNum = firstSection.Key;
            var sectionName = firstSection.Value.Name;

            var tracks = await mediaService.GetBiblePublicationTracks(languageCode, publicationCode, sectionNum);
            if (tracks != null && tracks.Count > 0)
            {
                var firstTrack = tracks.Values.OrderBy(t => t.Number).First();
                return (sectionCode, sectionName, firstTrack.Number, firstTrack.Title ?? string.Empty);
            }

            return (sectionCode, sectionName, 0, string.Empty);
        }

        // Flat publication - get tracks directly (sectionNumber = 0)
        var flatTracksWithLanguage = await mediaService.GetBiblePublicationTracks(languageCode, publicationCode, 0);
        if (flatTracksWithLanguage != null && flatTracksWithLanguage.Count > 0)
        {
            var firstTrack = flatTracksWithLanguage.Values.OrderBy(t => t.Number).First();
            return (null, string.Empty, firstTrack.Number, firstTrack.Title ?? string.Empty);
        }

        return (null, string.Empty, 0, string.Empty);
    }

    internal static int GetSectionNumberFromSectionCode(string sectionCode)
    {
        // Try to parse numeric section codes
        if (int.TryParse(sectionCode, out var num))
        {
            return num;
        }

        // Try to extract number from sectionCode like "iam-1"
        if (sectionCode.Contains('-'))
        {
            var parts = sectionCode.Split('-');
            if (parts.Length > 1 && int.TryParse(parts[parts.Length - 1], out var extractedNum))
            {
                return extractedNum;
            }
        }

        return 0;
    }
}

