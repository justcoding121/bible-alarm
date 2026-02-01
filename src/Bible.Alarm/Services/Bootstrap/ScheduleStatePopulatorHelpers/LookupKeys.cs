#nullable enable
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Services.Bootstrap.ScheduleStatePopulatorHelpers;

/// <summary>
/// Collects all unique keys needed for batch loading lookup data.
/// </summary>
internal sealed class LookupDataCollector
{
    public LookupKeys CollectKeys(List<AlarmSchedule> alarmSchedules)
    {
        var publicationKeys = new HashSet<(string LanguageCode, string PublicationCode)>();
        var sectionKeys = new HashSet<(string LanguageCode, string PublicationCode, string SectionCode)>();
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
                if (!string.IsNullOrWhiteSpace(br.LanguageCode) && !string.IsNullOrWhiteSpace(br.PublicationCode))
                {
                    publicationKeys.Add((br.LanguageCode, br.PublicationCode));
                    if (!string.IsNullOrWhiteSpace(br.SectionCode))
                    {
                        sectionKeys.Add((br.LanguageCode, br.PublicationCode, br.SectionCode));
                    }
                }
            }

            // Collect music keys
            if (schedule.Music != null)
            {
                var music = schedule.Music;
                if (music.MusicType == MusicType.VocalMusic)
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
                else if (music.MusicType == MusicType.Music)
                {
                    if (!string.IsNullOrWhiteSpace(music.PublicationCode))
                    {
                        melodyPublicationCodes.Add(music.PublicationCode);
                        if (!string.IsNullOrWhiteSpace(music.SectionCode))
                        {
                            melodySectionKeys.Add((music.PublicationCode, music.SectionCode));
                        }
                    }
                }
            }
        }

        return new LookupKeys(
            PublicationKeys: publicationKeys,
            SectionKeys: sectionKeys,
            VocalMusicLanguageCodes: vocalMusicLanguageCodes,
            VocalMusicKeys: vocalMusicKeys,
            VocalTrackKeys: vocalTrackKeys,
            MelodyPublicationCodes: melodyPublicationCodes,
            MelodySectionKeys: melodySectionKeys);
    }

    public sealed record LookupKeys(
        HashSet<(string LanguageCode, string PublicationCode)> PublicationKeys,
        HashSet<(string LanguageCode, string PublicationCode, string SectionCode)> SectionKeys,
        HashSet<string> VocalMusicLanguageCodes,
        HashSet<(string LanguageCode, string PublicationCode)> VocalMusicKeys,
        HashSet<(string LanguageCode, string PublicationCode)> VocalTrackKeys,
        HashSet<string> MelodyPublicationCodes,
        HashSet<(string PublicationCode, string SectionCode)> MelodySectionKeys);
}

