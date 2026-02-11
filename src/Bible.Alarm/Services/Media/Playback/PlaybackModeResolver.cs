#nullable enable

using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Serilog;

namespace Bible.Alarm.Services.Media.Playback;

/// <summary>
/// Resolves whether playback for a schedule is indefinite (NumberOfTracksToPlay &lt;= 0).
/// </summary>
public sealed class PlaybackModeResolver
{
    private readonly IAlarmScheduleService alarmScheduleService;
    private readonly ILogger logger;

    public PlaybackModeResolver(IAlarmScheduleService alarmScheduleService, ILogger logger)
    {
        this.alarmScheduleService = alarmScheduleService;
        this.logger = logger;
    }

    public async Task<bool> IsIndefinitePlaybackAsync(int scheduleId, CancellationToken cancellationToken)
    {
        try
        {
            var schedule = await alarmScheduleService.GetScheduleByIdAsync(
                scheduleId,
                includeMusic: false,
                includeBiblePublication: false,
                cancellationToken);

            return schedule != null && schedule.NumberOfTracksToPlay <= 0;
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to load schedule {ScheduleId} to determine indefinite playback; defaulting to finite", scheduleId);
            return false;
        }
    }
}
