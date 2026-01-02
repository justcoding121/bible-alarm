#nullable enable
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Shared.Models.Enums;
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
            if (!stateItem.MusicType.HasValue ||
                !stateItem.MusicTrackNumber.HasValue ||
                stateItem.MusicTrackNumber.Value <= 0)
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
            const string defaultPublicationCode = "iam";

            if (melodyMusicService == null)
            {
                return;
            }

            // Load default music once for all schedules
            var melodyMusic = await melodyMusicService.GetByCodeWithTracksAsync(defaultPublicationCode);

            if (melodyMusic?.Tracks == null || melodyMusic.Tracks.Count == 0)
            {
                Log.Logger.Warning("Melody music '{PublicationCode}' not found or has no tracks - cannot populate default music for {Count} schedules",
                    defaultPublicationCode, schedulesNeedingMusic.Count);
                return;
            }

            // Apply to all schedules needing music
            var random = new Random();
            foreach (var (schedule, stateItem) in schedulesNeedingMusic)
            {
                var randomTrack = melodyMusic.Tracks[random.Next(melodyMusic.Tracks.Count)];

                stateItem.MusicType = MusicType.Melodies;
                stateItem.MusicPublicationCode = defaultPublicationCode;
                stateItem.MusicLanguageCode = null;
                stateItem.MusicTrackNumber = randomTrack.Number;
                stateItem.MusicRepeat = false;
                stateItem.MusicTrackName = $"Melody Number(s) {randomTrack.Title}";

                Log.Logger.Debug("Populated default music properties for schedule {ScheduleId}. MusicType=Melodies, PublicationCode={PublicationCode}, TrackNumber={TrackNumber}",
                    schedule.Id, defaultPublicationCode, randomTrack.Number);
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

