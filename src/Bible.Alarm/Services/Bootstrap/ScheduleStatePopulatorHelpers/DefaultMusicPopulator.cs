#nullable enable
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
            if (melodyMusicService == null)
            {
                return;
            }

            // Get all melody music publications from database and use the first one
            var melodyReleases = await melodyMusicService.GetAllAsync();
            if (melodyReleases == null || melodyReleases.Count == 0)
            {
                Log.Logger.Warning("No melody music publications found in database - cannot populate default music for {Count} schedules",
                    schedulesNeedingMusic.Count);
                return;
            }

            var firstMelody = melodyReleases.FirstOrDefault();
            if (firstMelody.Value == null)
            {
                return;
            }

            var defaultPublicationCode = firstMelody.Key;
            var defaultPublicationName = firstMelody.Value.Name;

            // Load tracks for the default melody music
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

                stateItem.MusicPublicationCode = defaultPublicationCode;
                stateItem.MusicPublicationName = defaultPublicationName;
                stateItem.MusicLanguageCode = null;
                stateItem.MusicTrackNumber = randomTrack.Number;
                stateItem.MusicRepeat = false;
                // Titles for melody tracks should come from harvested track titles as-is.
                stateItem.MusicTrackName = randomTrack.Title;

                Log.Logger.Debug("Populated default music properties for schedule {ScheduleId}. PublicationCode={PublicationCode}, TrackNumber={TrackNumber}",
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

