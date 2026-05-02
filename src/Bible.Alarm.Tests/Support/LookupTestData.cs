#nullable enable

using Bible.Alarm.Services.Bootstrap.ScheduleStatePopulatorHelpers;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;

namespace Bible.Alarm.Tests.Support;

internal static class LookupTestData
{
    /// <summary>
    /// Empty lookup tables using the same comparers as production bootstrap loading.
    /// </summary>
    public static LookupDataLoader.LookupData EmptyLookup() =>
        new(
            Publications: new Dictionary<(string LanguageCode, string PublicationCode), BiblePublication>(
                PublicationLookupKeyComparers.LanguagePublication.Instance),
            Sections: new Dictionary<(string LanguageCode, string PublicationCode, string SectionCode), string>(
                PublicationLookupKeyComparers.LanguagePublicationSection.Instance),
            NoLanguagePublications: new Dictionary<string, LookupDataLoader.NoLanguagePublicationMeta>(
                StringComparer.OrdinalIgnoreCase),
            NoLanguageSections: new Dictionary<(string PublicationCode, string SectionCode), string>(
                PublicationLookupKeyComparers.PublicationSection.Instance),
            NoLanguageTrackTitles: new Dictionary<(string PublicationCode, string? SectionCode, string TrackCode), string>(
                PublicationLookupKeyComparers.PublicationNullableSectionTrack.Instance),
            VocalLanguages: new Dictionary<string, Language>(StringComparer.OrdinalIgnoreCase),
            VocalReleases: new Dictionary<(string LanguageCode, string PublicationCode), VocalMusic>(
                PublicationLookupKeyComparers.LanguagePublication.Instance),
            VocalTracks: new Dictionary<(string LanguageCode, string PublicationCode), SortedDictionary<int, MusicTrack>>(
                PublicationLookupKeyComparers.LanguagePublication.Instance),
            MelodyTracksFlat: new Dictionary<string, SortedDictionary<int, MusicTrack>>(StringComparer.OrdinalIgnoreCase),
            MelodyTracksBySection: new Dictionary<(string PublicationCode, string SectionCode), SortedDictionary<int, MusicTrack>>(
                PublicationLookupKeyComparers.PublicationSection.Instance),
            MelodyReleases: new Dictionary<string, MelodyMusic>(StringComparer.OrdinalIgnoreCase));
}
