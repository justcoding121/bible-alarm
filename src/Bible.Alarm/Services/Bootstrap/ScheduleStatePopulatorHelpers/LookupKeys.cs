#nullable enable
using Bible.Alarm.Shared.Models.Enums;

namespace Bible.Alarm.Services.Bootstrap.ScheduleStatePopulatorHelpers;

/// <summary>
/// Collects all unique keys needed for batch loading lookup data.
/// </summary>
internal sealed class LookupDataCollector
{
    public LookupKeys CollectKeys(List<AlarmSchedule> alarmSchedules)
    {
        var translationKeys = new HashSet<(string LanguageCode, string PublicationCode)>();
        var sectionKeys = new HashSet<(string LanguageCode, string PublicationCode, int SectionNumber)>();
        var vocalMusicLanguageCodes = new HashSet<string>();
        var vocalMusicKeys = new HashSet<(string LanguageCode, string PublicationCode)>();
        var vocalTrackKeys = new HashSet<(string LanguageCode, string PublicationCode)>();
        var melodyPublicationCodes = new HashSet<string>();

        foreach (var schedule in alarmSchedules)
        {
            // Collect Bible reading keys
            if (schedule.BiblePublicationSchedule != null)
            {
                var br = schedule.BiblePublicationSchedule;
                if (!string.IsNullOrWhiteSpace(br.LanguageCode) && !string.IsNullOrWhiteSpace(br.PublicationCode))
                {
                    translationKeys.Add((br.LanguageCode, br.PublicationCode));
                    if (br.SectionNumber.HasValue && br.SectionNumber.Value > 0)
                    {
                        sectionKeys.Add((br.LanguageCode, br.PublicationCode, br.SectionNumber.Value));
                    }
                }
            }

            // Collect music keys
            if (schedule.Music != null)
            {
                var music = schedule.Music;
                if (music.MusicType == MusicType.Vocals)
                {
                    if (!string.IsNullOrWhiteSpace(music.LanguageCode))
                    {
                        vocalMusicLanguageCodes.Add(music.LanguageCode);
                        if (!string.IsNullOrWhiteSpace(music.PublicationCode))
                        {
                            vocalMusicKeys.Add((music.LanguageCode, music.PublicationCode));
                            if (music.TrackNumber > 0)
                            {
                                vocalTrackKeys.Add((music.LanguageCode, music.PublicationCode));
                            }
                        }
                    }
                }
                else if (music.MusicType == MusicType.Melodies)
                {
                    if (!string.IsNullOrWhiteSpace(music.PublicationCode))
                    {
                        melodyPublicationCodes.Add(music.PublicationCode);
                    }
                }
            }
        }

        return new LookupKeys(
            TranslationKeys: translationKeys,
            SectionKeys: sectionKeys,
            VocalMusicLanguageCodes: vocalMusicLanguageCodes,
            VocalMusicKeys: vocalMusicKeys,
            VocalTrackKeys: vocalTrackKeys,
            MelodyPublicationCodes: melodyPublicationCodes);
    }

    public sealed record LookupKeys(
        HashSet<(string LanguageCode, string PublicationCode)> TranslationKeys,
        HashSet<(string LanguageCode, string PublicationCode, int SectionNumber)> SectionKeys,
        HashSet<string> VocalMusicLanguageCodes,
        HashSet<(string LanguageCode, string PublicationCode)> VocalMusicKeys,
        HashSet<(string LanguageCode, string PublicationCode)> VocalTrackKeys,
        HashSet<string> MelodyPublicationCodes);
}

