#nullable enable
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;

namespace Bible.Alarm.Services.Media.Playlist;

/// <summary>
/// Manages schedule retrieval and persistence for PlaylistService.
/// Separated from PlaylistService for better modularity.
/// </summary>
public class PlaylistScheduleManager
{
    private readonly IAlarmScheduleService alarmScheduleService;
    private readonly IGeneralSettingsService generalSettingsService;
    private readonly CancellationToken cancellationToken;

    public PlaylistScheduleManager(
        IAlarmScheduleService alarmScheduleService,
        IGeneralSettingsService generalSettingsService,
        CancellationToken cancellationToken)
    {
        this.alarmScheduleService = alarmScheduleService;
        this.generalSettingsService = generalSettingsService;
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
            // Do NOT create schedules here.
            // Schedule creation/seeding belongs to bootstrap (`IDatabaseSeedService`) and the UI "Add schedule" flow.
            // Creating schedules from playback logic can cause duplicated schedules on fresh installs.
            throw new InvalidOperationException(
                "No schedules found in database. Bootstrap/seeding should have ensured at least one schedule exists.");
        }

        return schedule.Id;
    }

    public async Task SaveLastPlayed(int currentScheduleId)
    {
        await generalSettingsService.SetGeneralSettingAsync(
            AppConstants.GeneralSettingsKeys.LastPlayedScheduleId,
            currentScheduleId.ToString(),
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

