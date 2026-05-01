#nullable enable
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Schedule;

namespace Bible.Alarm.Services.Bootstrap.ScheduleStatePopulatorHelpers;

/// <summary>
/// Collects all unique keys needed for batch loading lookup data.
/// Music type is inferred from LanguageCode: NULL/empty = instrumental (melody), otherwise = vocal.
/// </summary>
internal static class LookupDataCollector
{
    public static LookupKeys CollectKeys(List<AlarmSchedule> alarmSchedules)
    {
        var publicationKeys =
            new HashSet<(string LanguageCode, string PublicationCode)>(PublicationLookupKeyComparers.LanguagePublication.Instance);
        var sectionKeys = new HashSet<(string LanguageCode, string PublicationCode, string SectionCode)>(
            PublicationLookupKeyComparers.LanguagePublicationSection.Instance);
        var bibleTrackKeys =
            new HashSet<(string LanguageCode, string PublicationCode, string? SectionCode, string TrackCode)>(
                PublicationLookupKeyComparers.LanguagePublicationNullableSectionTrack.Instance);
        var vocalMusicLanguageCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var vocalMusicKeys =
            new HashSet<(string LanguageCode, string PublicationCode)>(PublicationLookupKeyComparers.LanguagePublication.Instance);
        var vocalTrackKeys =
            new HashSet<(string LanguageCode, string PublicationCode)>(PublicationLookupKeyComparers.LanguagePublication.Instance);
        var melodyPublicationCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var melodySectionKeys = new HashSet<(string PublicationCode, string SectionCode)>(
            PublicationLookupKeyComparers.PublicationSection.Instance);

        foreach (var schedule in alarmSchedules)
        {
            if (schedule.BiblePublicationSchedule != null)
            {
                CollectBiblePublicationKeys(schedule.BiblePublicationSchedule, publicationKeys, sectionKeys, bibleTrackKeys);
            }

            if (schedule.Music != null)
            {
                CollectMusicKeys(schedule.Music, vocalMusicLanguageCodes, vocalMusicKeys, vocalTrackKeys, melodyPublicationCodes, melodySectionKeys);
            }
        }

        return new LookupKeys(
            PublicationKeys: publicationKeys,
            SectionKeys: sectionKeys,
            BibleTrackKeys: bibleTrackKeys,
            VocalMusicLanguageCodes: vocalMusicLanguageCodes,
            VocalMusicKeys: vocalMusicKeys,
            VocalTrackKeys: vocalTrackKeys,
            MelodyPublicationCodes: melodyPublicationCodes,
            MelodySectionKeys: melodySectionKeys);
    }

    private static void CollectBiblePublicationKeys(
        BiblePublicationSchedule br,
        HashSet<(string LanguageCode, string PublicationCode)> publicationKeys,
        HashSet<(string LanguageCode, string PublicationCode, string SectionCode)> sectionKeys,
        HashSet<(string LanguageCode, string PublicationCode, string? SectionCode, string TrackCode)> bibleTrackKeys)
    {
        if (string.IsNullOrWhiteSpace(br.PublicationCode))
        {
            return;
        }

        // For non-language publications (LanguageCode is null in DB), use default language code during bootstrap
        // This ensures display names are populated for publications like "iam" that have LanguageId == null
        var effectiveLanguageCode = string.IsNullOrWhiteSpace(br.LanguageCode) ? AppConstants.Media.DefaultLanguageCode : br.LanguageCode;

        publicationKeys.Add((effectiveLanguageCode, br.PublicationCode));
        var normalizedSectionCode = SectionCodeHelper.Normalize(br.SectionCode);
        if (!string.IsNullOrWhiteSpace(normalizedSectionCode))
        {
            sectionKeys.Add((effectiveLanguageCode, br.PublicationCode, normalizedSectionCode));
        }

        if (!string.IsNullOrWhiteSpace(br.TrackCode))
        {
            bibleTrackKeys.Add((effectiveLanguageCode, br.PublicationCode, normalizedSectionCode, br.TrackCode));
        }
    }

    private static void CollectMusicKeys(
        AlarmMusic music,
        HashSet<string> vocalMusicLanguageCodes,
        HashSet<(string LanguageCode, string PublicationCode)> vocalMusicKeys,
        HashSet<(string LanguageCode, string PublicationCode)> vocalTrackKeys,
        HashSet<string> melodyPublicationCodes,
        HashSet<(string PublicationCode, string SectionCode)> melodySectionKeys)
    {
        // Music type is inferred: NULL/empty LanguageCode = melody (instrumental), otherwise = vocal
        var isMelodyMusic = string.IsNullOrEmpty(music.LanguageCode);

        if (!isMelodyMusic)
        {
            vocalMusicLanguageCodes.Add(music.LanguageCode!);
            if (!string.IsNullOrWhiteSpace(music.PublicationCode))
            {
                vocalMusicKeys.Add((music.LanguageCode!, music.PublicationCode));
                if (!string.IsNullOrWhiteSpace(music.TrackCode))
                {
                    vocalTrackKeys.Add((music.LanguageCode!, music.PublicationCode));
                }
            }

            return;
        }

        if (!string.IsNullOrWhiteSpace(music.PublicationCode))
        {
            melodyPublicationCodes.Add(music.PublicationCode);
            var normalizedSectionCode = SectionCodeHelper.Normalize(music.SectionCode);
            if (!string.IsNullOrWhiteSpace(normalizedSectionCode))
            {
                melodySectionKeys.Add((music.PublicationCode, normalizedSectionCode));
            }
        }
    }

    public sealed record LookupKeys(
        HashSet<(string LanguageCode, string PublicationCode)> PublicationKeys,
        HashSet<(string LanguageCode, string PublicationCode, string SectionCode)> SectionKeys,
        HashSet<(string LanguageCode, string PublicationCode, string? SectionCode, string TrackCode)> BibleTrackKeys,
        HashSet<string> VocalMusicLanguageCodes,
        HashSet<(string LanguageCode, string PublicationCode)> VocalMusicKeys,
        HashSet<(string LanguageCode, string PublicationCode)> VocalTrackKeys,
        HashSet<string> MelodyPublicationCodes,
        HashSet<(string PublicationCode, string SectionCode)> MelodySectionKeys);
}

