#nullable enable
using Bible;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Storage.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Serilog;

namespace Bible.Alarm.Services.Media.Playlist;

/// <summary>
/// Manages schedule retrieval and persistence for PlaylistService.
/// Separated from PlaylistService for better modularity.
/// </summary>
public class PlaylistScheduleManager
{
    private readonly ILogger logger;
    private readonly IAlarmScheduleService alarmScheduleService;
    private readonly IGeneralSettingsService generalSettingsService;
    private readonly IBiblePublicationService BiblePublicationService;
    private readonly IMelodyMusicService melodyMusicService;
    private readonly CancellationToken cancellationToken;

    public PlaylistScheduleManager(
        ILogger logger,
        IAlarmScheduleService alarmScheduleService,
        IGeneralSettingsService generalSettingsService,
        IBiblePublicationService BiblePublicationService,
        IMelodyMusicService melodyMusicService,
        CancellationToken cancellationToken)
    {
        this.logger = logger;
        this.alarmScheduleService = alarmScheduleService;
        this.generalSettingsService = generalSettingsService;
        this.BiblePublicationService = BiblePublicationService;
        this.melodyMusicService = melodyMusicService;
        this.cancellationToken = cancellationToken;
    }

    public async Task<int> GetRelevantScheduleToPlay()
    {
        var lastSchedule = await generalSettingsService.GetGeneralSettingAsync(
            AppConstants.GeneralSettingsKeys.LastPlayedScheduleId, cancellationToken);

        AlarmSchedule? schedule = null;

        if (!string.IsNullOrEmpty(lastSchedule?.Value))
        {
            schedule = await alarmScheduleService.GetScheduleByIdAsync(
                int.Parse(lastSchedule.Value), false, false, cancellationToken);
        }

        if (schedule == null)
        {
            schedule = await alarmScheduleService.GetFirstScheduleOrDefaultAsync(
                false, false, cancellationToken);
        }

        if (schedule == null)
        {
            schedule = await AlarmSchedule.GetSampleSchedule(false, BiblePublicationService, melodyMusicService);
            schedule = await alarmScheduleService.AddScheduleAsync(schedule, cancellationToken);
        }

        return schedule.Id;
    }

    public async Task SaveLastPlayed(int scheduleId)
    {
        await generalSettingsService.SetGeneralSettingAsync(
            AppConstants.GeneralSettingsKeys.LastPlayedScheduleId,
            scheduleId.ToString(),
            cancellationToken);
    }

    public async Task<AlarmSchedule> LoadScheduleForTracks(int scheduleId)
    {
        return await alarmScheduleService.GetScheduleByIdAsync(
            scheduleId, true, true, cancellationToken) ??
            throw new ArgumentException($"Invalid schedule Id {scheduleId}");
    }

    public static void ValidateScheduleId(int scheduleId)
    {
        if (scheduleId <= 0)
        {
            throw new ArgumentException($"Invalid schedule Id {scheduleId}. Schedule ID must be greater than 0.");
        }
    }
}

