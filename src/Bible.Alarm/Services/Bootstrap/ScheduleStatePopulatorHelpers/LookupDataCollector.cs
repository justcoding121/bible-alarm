#nullable enable
using Bible.Alarm.Models.Schedule;
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
        var bookKeys = new HashSet<(string LanguageCode, string PublicationCode, int BookNumber)>();
        var vocalMusicLanguageCodes = new HashSet<string>();
        var vocalMusicKeys = new HashSet<(string LanguageCode, string PublicationCode)>();
        var vocalTrackKeys = new HashSet<(string LanguageCode, string PublicationCode)>();
        var melodyPublicationCodes = new HashSet<string>();

        foreach (var schedule in alarmSchedules)
        {
            // Collect Bible reading keys
            if (schedule.BibleReadingSchedule != null)
            {
                var br = schedule.BibleReadingSchedule;
                if (!string.IsNullOrWhiteSpace(br.LanguageCode) && !string.IsNullOrWhiteSpace(br.PublicationCode))
                {
                    translationKeys.Add((br.LanguageCode, br.PublicationCode));
                    if (br.BookNumber.HasValue && br.BookNumber.Value > 0)
                    {
                        bookKeys.Add((br.LanguageCode, br.PublicationCode, br.BookNumber.Value));
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
            BookKeys: bookKeys,
            VocalMusicLanguageCodes: vocalMusicLanguageCodes,
            VocalMusicKeys: vocalMusicKeys,
            VocalTrackKeys: vocalTrackKeys,
            MelodyPublicationCodes: melodyPublicationCodes);
    }

    public sealed record LookupKeys(
        HashSet<(string LanguageCode, string PublicationCode)> TranslationKeys,
        HashSet<(string LanguageCode, string PublicationCode, int BookNumber)> BookKeys,
        HashSet<string> VocalMusicLanguageCodes,
        HashSet<(string LanguageCode, string PublicationCode)> VocalMusicKeys,
        HashSet<(string LanguageCode, string PublicationCode)> VocalTrackKeys,
        HashSet<string> MelodyPublicationCodes);
}

