#nullable enable
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Services.Bootstrap.ScheduleStatePopulatorHelpers;

/// <summary>
/// Collects all unique keys needed for batch loading lookup data.
/// Music type is inferred from LanguageCode: NULL/empty = instrumental (melody), otherwise = vocal.
/// </summary>
internal sealed class LookupDataCollector
{
    public LookupKeys CollectKeys(List<AlarmSchedule> alarmSchedules)
    {
        var publicationKeys = new HashSet<(string LanguageCode, string PublicationCode)>();
        var sectionKeys = new HashSet<(string LanguageCode, string PublicationCode, string SectionCode)>();
        var bibleTrackKeys = new HashSet<(string LanguageCode, string PublicationCode, string? SectionCode, string TrackCode)>();
        var vocalMusicLanguageCodes = new HashSet<string>();
        var vocalMusicKeys = new HashSet<(string LanguageCode, string PublicationCode)>();
        var vocalTrackKeys = new HashSet<(string LanguageCode, string PublicationCode)>();
        var melodyPublicationCodes = new HashSet<string>();
        var melodySectionKeys = new HashSet<(string PublicationCode, string SectionCode)>();

        foreach (var schedule in alarmSchedules)
        {
            // Collect Bible reading keys
            if (schedule.BiblePublicationSchedule != null)
            {
                var br = schedule.BiblePublicationSchedule;
                if (!string.IsNullOrWhiteSpace(br.PublicationCode))
                {
                    // For non-language publications (LanguageCode is null in DB), use "E" as default during bootstrap
                    // This ensures display names are populated for publications like "iam" that have LanguageId == null
                    var effectiveLanguageCode = string.IsNullOrWhiteSpace(br.LanguageCode) ? "E" : br.LanguageCode;
                    
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
            }

            // Collect music keys
            // Music type is inferred: NULL/empty LanguageCode = melody (instrumental), otherwise = vocal
            if (schedule.Music != null)
            {
                var music = schedule.Music;
                var isMelodyMusic = string.IsNullOrEmpty(music.LanguageCode);
                
                if (!isMelodyMusic)
                {
                    // Vocal music (has language code)
                    vocalMusicLanguageCodes.Add(music.LanguageCode!);
                    if (!string.IsNullOrWhiteSpace(music.PublicationCode))
                    {
                        vocalMusicKeys.Add((music.LanguageCode!, music.PublicationCode));
                        if (!string.IsNullOrWhiteSpace(music.TrackCode))
                        {
                            vocalTrackKeys.Add((music.LanguageCode!, music.PublicationCode));
                        }
                    }
                }
                else
                {
                    // Melody/instrumental music (no language code)
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

