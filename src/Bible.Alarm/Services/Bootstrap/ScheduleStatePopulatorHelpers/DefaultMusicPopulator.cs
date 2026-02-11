#nullable enable
using System.Security.Cryptography;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores.Models;
using Serilog;

namespace Bible.Alarm.Services.Bootstrap.ScheduleStatePopulatorHelpers;

/// <summary>
/// Populates default music for schedules that don't have music configured.
/// </summary>
internal sealed class DefaultMusicPopulator
{
    private readonly IMelodyMusicService? melodyMusicService;

    public DefaultMusicPopulator(IMelodyMusicService? melodyMusicService)
    {
        this.melodyMusicService = melodyMusicService;
    }

    public async Task PopulateBatchAsync(
        List<AlarmSchedule> alarmSchedules,
        ScheduleStateItem[] scheduleStateItems)
    {
        // Identify schedules that need default music
        var schedulesNeedingMusic = new List<(AlarmSchedule Schedule, ScheduleStateItem StateItem)>();
        for (int i = 0; i < alarmSchedules.Count && i < scheduleStateItems.Length; i++)
        {
            var stateItem = scheduleStateItems[i];
            if (string.IsNullOrEmpty(stateItem.MusicPublicationCode) ||
                string.IsNullOrWhiteSpace(stateItem.MusicTrackCode))
            {
                schedulesNeedingMusic.Add((alarmSchedules[i], stateItem));
            }
        }

        if (schedulesNeedingMusic.Count == 0)
        {
            return;
        }

        try
        {
            if (melodyMusicService == null)
            {
                return;
            }

            // Get all melody music publications; prefer "iam" (Kingdom Melodies) as default, else first available
            const string PreferredMelodyPublicationCode = "iam";
            var melodyReleases = await melodyMusicService.GetAllAsync();
            if (melodyReleases == null || melodyReleases.Count == 0)
            {
                Log.Logger.Warning("No melody music publications found in database - cannot populate default music for {Count} schedules",
                    schedulesNeedingMusic.Count);
                return;
            }

            string defaultPublicationCode;
            string defaultPublicationName;
            if (melodyReleases.TryGetValue(PreferredMelodyPublicationCode, out var preferred) && preferred != null)
            {
                defaultPublicationCode = PreferredMelodyPublicationCode;
                defaultPublicationName = preferred.Name;
            }
            else
            {
                var firstMelody = melodyReleases.FirstOrDefault();
                if (firstMelody.Value == null)
                {
                    return;
                }
                defaultPublicationCode = firstMelody.Key;
                defaultPublicationName = firstMelody.Value.Name;
            }

            // Load tracks for the default melody music
            var melodyMusic = await melodyMusicService.GetByCodeWithTracksAsync(defaultPublicationCode);

            if (melodyMusic?.Tracks == null || melodyMusic.Tracks.Count == 0)
            {
                Log.Logger.Warning("Melody music '{PublicationCode}' not found or has no tracks - cannot populate default music for {Count} schedules",
                    defaultPublicationCode, schedulesNeedingMusic.Count);
                return;
            }

            var trackCount = melodyMusic.Tracks.Count;
            foreach (var (schedule, stateItem) in schedulesNeedingMusic)
            {
                var randomTrack = melodyMusic.Tracks[RandomNumberGenerator.GetInt32(trackCount)];

                stateItem.MusicPublicationCode = defaultPublicationCode;
                stateItem.MusicPublicationName = defaultPublicationName;
                stateItem.MusicLanguageCode = null;
                stateItem.MusicTrackCode = TrackCodeHelper.GetFromTrack(randomTrack);
                stateItem.MusicRepeat = false;
                // Titles for melody tracks should come from harvested track titles as-is.
                stateItem.MusicTrackName = randomTrack.Title;

                Log.Logger.Debug("Populated default music properties for schedule {ScheduleId}. PublicationCode={PublicationCode}, TrackCode={TrackCode}",
                    schedule.Id, defaultPublicationCode, TrackCodeHelper.GetFromTrack(randomTrack));
            }

            Log.Logger.Information("Batch populated default music for {Count} schedules", schedulesNeedingMusic.Count);
        }
        catch (Exception defaultMusicEx)
        {
            Log.Logger.Warning(defaultMusicEx, "Error batch populating default music properties for {Count} schedules",
                schedulesNeedingMusic.Count);
        }
    }
}

