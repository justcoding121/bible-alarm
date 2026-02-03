#nullable enable
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Stores.Models;
using Serilog;

namespace Bible.Alarm.Services.Bootstrap.ScheduleStatePopulatorHelpers;

/// <summary>
/// Populates music display names from cached lookup data.
/// </summary>
internal sealed class MusicDisplayNamePopulator
{
    public void Populate(
        AlarmSchedule schedule,
        ScheduleStateItem scheduleStateItem,
        LookupDataLoader.LookupData lookupData)
    {
        if (schedule.Music == null)
        {
            return;
        }

        var music = schedule.Music;

        if (music.MusicType == MusicType.VocalMusic)
        {
            PopulateVocalMusic(schedule, scheduleStateItem, music, lookupData);
        }
        else if (music.MusicType == MusicType.Music)
        {
            PopulateMelodyMusic(schedule, scheduleStateItem, music, lookupData);
        }
    }

    private static void PopulateVocalMusic(
        AlarmSchedule schedule,
        ScheduleStateItem scheduleStateItem,
        AlarmMusic music,
        LookupDataLoader.LookupData lookupData)
    {
        // Use cached vocal languages
        if (!string.IsNullOrWhiteSpace(music.LanguageCode))
        {
            if (lookupData.VocalLanguages.TryGetValue(music.LanguageCode, out var vocalLanguage))
            {
                scheduleStateItem.MusicLanguageName = vocalLanguage.Name;
                scheduleStateItem.MusicLanguageDirection = vocalLanguage.Direction;
                Log.Logger.Debug("Set MusicLanguageName '{MusicLanguageName}' and MusicLanguageDirection '{MusicLanguageDirection}' for schedule {ScheduleId} (LanguageCode: {LanguageCode})",
                    vocalLanguage.Name, vocalLanguage.Direction, schedule.Id, music.LanguageCode);
            }
            else
            {
                scheduleStateItem.MusicLanguageName = music.LanguageCode;
                // Default to LTR if language not found
                scheduleStateItem.MusicLanguageDirection = "ltr";
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
        if (music.TrackNumber > 0 &&
            !string.IsNullOrWhiteSpace(music.LanguageCode) &&
            !string.IsNullOrWhiteSpace(music.PublicationCode))
        {
            var trackKey = (music.LanguageCode, music.PublicationCode);
            if (lookupData.VocalTracks.TryGetValue(trackKey, out var tracks) &&
                tracks.TryGetValue(music.TrackNumber, out var track))
            {
                scheduleStateItem.MusicTrackName = track.Title;
                Log.Logger.Debug("Set MusicTrackName '{MusicTrackName}' for schedule {ScheduleId} (TrackNumber: {TrackNumber})",
                    track.Title, schedule.Id, music.TrackNumber);
            }
        }
    }

    private static void PopulateMelodyMusic(
        AlarmSchedule schedule,
        ScheduleStateItem scheduleStateItem,
        AlarmMusic music,
        LookupDataLoader.LookupData lookupData)
    {
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
        if (!string.IsNullOrWhiteSpace(music.PublicationCode) && music.TrackNumber > 0)
        {
            if (!string.IsNullOrWhiteSpace(music.SectionCode) &&
                lookupData.MelodyTracksBySection.TryGetValue((music.PublicationCode, music.SectionCode), out var sectionTracks) &&
                sectionTracks.TryGetValue(music.TrackNumber, out var sectionTrack))
            {
                // Titles for melody tracks should come from harvested track titles as-is.
                scheduleStateItem.MusicTrackName = sectionTrack.Title;
                Log.Logger.Debug("Set MusicTrackName '{MusicTrackName}' for schedule {ScheduleId} (TrackNumber: {TrackNumber})",
                    sectionTrack.Title, schedule.Id, music.TrackNumber);
                return;
            }

            if (lookupData.MelodyTracksFlat.TryGetValue(music.PublicationCode, out var flatTracks) &&
                flatTracks.TryGetValue(music.TrackNumber, out var flatTrack))
            {
                // Titles for melody tracks should come from harvested track titles as-is.
                scheduleStateItem.MusicTrackName = flatTrack.Title;
                Log.Logger.Debug("Set MusicTrackName '{MusicTrackName}' for schedule {ScheduleId} (TrackNumber: {TrackNumber})",
                    flatTrack.Title, schedule.Id, music.TrackNumber);
            }
        }
    }
}

