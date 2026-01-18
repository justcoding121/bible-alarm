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
                scheduleStateItem.MusicLanguageDirection = "ltr"; // Default to LTR if language not found
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
        if (!string.IsNullOrWhiteSpace(music.PublicationCode) && music.TrackNumber > 0)
        {
            // Use cached melody tracks
            if (lookupData.MelodyTracks.TryGetValue(music.PublicationCode, out var tracks) &&
                tracks.TryGetValue(music.TrackNumber, out var track))
            {
                scheduleStateItem.MusicTrackName = $"Melody Number(s) {track.Title}";
                Log.Logger.Debug("Set MusicTrackName '{MusicTrackName}' for schedule {ScheduleId} (TrackNumber: {TrackNumber})",
                    track.Title, schedule.Id, music.TrackNumber);
            }
        }
    }
}

