#nullable enable
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Stores.Models;
using Serilog;

namespace Bible.Alarm.Services.Bootstrap.ScheduleStatePopulatorHelpers;

/// <summary>
/// Populates music display names from cached lookup data.
/// Music type is inferred from LanguageCode: NULL/empty = instrumental (melody), otherwise = vocal.
/// </summary>
internal sealed class MusicDisplayNamePopulator
{
    public void Populate(
        AlarmSchedule schedule,
        ScheduleStateItem scheduleStateItem,
        LookupDataLoader.LookupData lookupData,
        Dictionary<string, string>? languageNamesByCode = null)
    {
        if (schedule.Music == null)
        {
            return;
        }

        var music = schedule.Music;
        
        // Music type is inferred: NULL/empty LanguageCode = melody (instrumental), otherwise = vocal
        var isMelodyMusic = string.IsNullOrEmpty(music.LanguageCode);

        if (!isMelodyMusic)
        {
            PopulateVocalMusic(schedule, scheduleStateItem, music, lookupData, languageNamesByCode);
        }
        else
        {
            PopulateMelodyMusic(schedule, scheduleStateItem, music, lookupData, languageNamesByCode);
        }
    }

    private static string GetLanguageDisplayName(string languageCode, Dictionary<string, string>? languageNamesByCode)
    {
        if (languageNamesByCode != null && languageNamesByCode.TryGetValue(languageCode, out var name))
        {
            return name;
        }
        return languageCode;
    }

    private static void PopulateVocalMusic(
        AlarmSchedule schedule,
        ScheduleStateItem scheduleStateItem,
        AlarmMusic music,
        LookupDataLoader.LookupData lookupData,
        Dictionary<string, string>? languageNamesByCode = null)
    {
        // Use cached vocal languages
        if (!string.IsNullOrWhiteSpace(music.LanguageCode))
        {
            if (lookupData.VocalLanguages.TryGetValue(music.LanguageCode, out var vocalLanguage))
            {
                scheduleStateItem.MusicLanguageName = GetLanguageDisplayName(music.LanguageCode, languageNamesByCode);
                scheduleStateItem.MusicLanguageDirection = vocalLanguage.Direction;
                Log.Logger.Debug("Set MusicLanguageName '{MusicLanguageName}' and MusicLanguageDirection '{MusicLanguageDirection}' for schedule {ScheduleId} (LanguageCode: {LanguageCode})",
                    scheduleStateItem.MusicLanguageName, vocalLanguage.Direction, schedule.Id, music.LanguageCode);
            }
            else
            {
                scheduleStateItem.MusicLanguageName = music.LanguageCode;
                // Default to LTR if language not found
                scheduleStateItem.MusicLanguageDirection = AppConstants.Media.TextDirectionLeftToRight;
            }
        }

        // Use cached vocal releases
        if (!string.IsNullOrWhiteSpace(music.PublicationCode) && !string.IsNullOrWhiteSpace(music.LanguageCode))
        {
            var releaseKey = (music.LanguageCode, music.PublicationCode);
            if (lookupData.VocalReleases.TryGetValue(releaseKey, out var release))
            {
                scheduleStateItem.MusicPublicationName = release.Name;
                Log.Logger.Debug("Set MusicPublicationName '{MusicPublicationName}' for schedule {ScheduleId} (PublicationCode: {PublicationCode})",
                    release.Name, schedule.Id, music.PublicationCode);
            }
        }

        // Populate vocal music section name if section code exists
        if (!string.IsNullOrWhiteSpace(music.SectionCode) &&
            !string.IsNullOrWhiteSpace(music.LanguageCode) &&
            !string.IsNullOrWhiteSpace(music.PublicationCode))
        {
            // For vocal music with sections, we need to get the section name from the BiblePublication
            // Since vocals are BiblePublications with Category=Music and LanguageId!=null,
            // we can use the same section lookup mechanism
            // Note: Music sections use SectionCode (string) which may need conversion
            Log.Logger.Debug(
                "Vocal music section code '{SectionCode}' found for schedule {ScheduleId} (LanguageCode: {LanguageCode}, PublicationCode: {PublicationCode}), section name lookup needed",
                music.SectionCode,
                schedule.Id,
                music.LanguageCode,
                music.PublicationCode);
        }

        // Use cached vocal tracks
        if (!string.IsNullOrWhiteSpace(music.TrackCode) &&
            !string.IsNullOrWhiteSpace(music.LanguageCode) &&
            !string.IsNullOrWhiteSpace(music.PublicationCode))
        {
            var trackKey = (music.LanguageCode, music.PublicationCode);
            if (lookupData.VocalTracks.TryGetValue(trackKey, out var tracks) &&
                MusicTrackLookupHelper.TryGetByCode(tracks, music.TrackCode, out var vocalPair))
            {
                scheduleStateItem.MusicTrackName = vocalPair.Track.Title;
                Log.Logger.Debug("Set MusicTrackName '{MusicTrackName}' for schedule {ScheduleId} (TrackCode: {TrackCode})",
                    vocalPair.Track.Title, schedule.Id, music.TrackCode);
            }
        }
    }

    private static void PopulateMelodyMusic(
        AlarmSchedule schedule,
        ScheduleStateItem scheduleStateItem,
        AlarmMusic music,
        LookupDataLoader.LookupData lookupData,
        Dictionary<string, string>? languageNamesByCode = null)
    {
        // No-language (melody) music: show "English" in the language row, same as Bible container for no-language publications.
        // Effective language for display and for publication modal is "E" (English + no-language pubs together).
        if (lookupData.VocalLanguages.TryGetValue(AppConstants.Media.DefaultLanguageCode, out var englishLanguage))
        {
            scheduleStateItem.MusicLanguageName = GetLanguageDisplayName(AppConstants.Media.DefaultLanguageCode, languageNamesByCode);
            scheduleStateItem.MusicLanguageDirection = englishLanguage.Direction ?? AppConstants.Media.TextDirectionLeftToRight;
            Log.Logger.Debug("Set MusicLanguageName '{MusicLanguageName}' for melody (no-language) in schedule {ScheduleId} (effective display: English)",
                scheduleStateItem.MusicLanguageName, schedule.Id);
        }

        // Populate melody publication name
        if (!string.IsNullOrWhiteSpace(music.PublicationCode))
        {
            if (lookupData.MelodyReleases.TryGetValue(music.PublicationCode, out var melodyRelease))
            {
                scheduleStateItem.MusicPublicationName = melodyRelease.Name;
                Log.Logger.Debug("Set MusicPublicationName '{MusicPublicationName}' for schedule {ScheduleId} (PublicationCode: {PublicationCode})",
                    melodyRelease.Name, schedule.Id, music.PublicationCode);
            }
        }

        // Use cached melody tracks.
        // For sectioned melody publications (e.g. "iam"), keys are (PublicationCode, SectionCode) to avoid ambiguity.
        if (!string.IsNullOrWhiteSpace(music.PublicationCode) && !string.IsNullOrWhiteSpace(music.TrackCode))
        {
            if (!string.IsNullOrWhiteSpace(music.SectionCode) &&
                lookupData.MelodyTracksBySection.TryGetValue((music.PublicationCode, music.SectionCode), out var sectionTracks) &&
                MusicTrackLookupHelper.TryGetByCode(sectionTracks, music.TrackCode, out var sectionPair))
            {
                scheduleStateItem.MusicTrackName = sectionPair.Track.Title;
                Log.Logger.Debug("Set MusicTrackName '{MusicTrackName}' for schedule {ScheduleId} (TrackCode: {TrackCode})",
                    sectionPair.Track.Title, schedule.Id, music.TrackCode);
                return;
            }

            if (lookupData.MelodyTracksFlat.TryGetValue(music.PublicationCode, out var flatTracks) &&
                MusicTrackLookupHelper.TryGetByCode(flatTracks, music.TrackCode, out var flatPair))
            {
                scheduleStateItem.MusicTrackName = flatPair.Track.Title;
                Log.Logger.Debug("Set MusicTrackName '{MusicTrackName}' for schedule {ScheduleId} (TrackCode: {TrackCode})",
                    flatPair.Track.Title, schedule.Id, music.TrackCode);
            }
        }
    }
}

