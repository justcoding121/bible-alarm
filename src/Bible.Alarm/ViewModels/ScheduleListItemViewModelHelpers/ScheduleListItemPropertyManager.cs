#nullable enable
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Schedule;
using Serilog;

namespace Bible.Alarm.ViewModels.ScheduleListItemViewModelHelpers;

/// <summary>
/// Handles property management for ScheduleListItemViewModel.
/// </summary>
public sealed class ScheduleListItemPropertyManager(
    ILogger logger,
    IScheduleStateService scheduleStateService)
{
    private bool isEnabled;
    private bool isInitializing;

    public bool IsEnabled
    {
        get => isEnabled;
        set => isEnabled = value;
    }

    public bool IsInitializing
    {
        get => isInitializing;
        set => isInitializing = value;
    }

    /// <summary>
    /// Handles IsEnabled property change.
    /// </summary>
    public async Task HandleIsEnabledChanged(int scheduleId, bool newValue, AlarmSchedule? schedule, Action notifyThisPropertyChanged, Action notifyPropertiesChanged, Func<bool, Task> revertChange)
    {
        try
        {
            notifyThisPropertyChanged();
            var success = await scheduleStateService.UpdateScheduleEnabledStateAsync(scheduleId, newValue);

            if (!success)
            {
                await revertChange(newValue);
            }
            else
            {
                notifyPropertiesChanged();
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "An error occurred while handling IsEnabled change for schedule {ScheduleId}", scheduleId);
            await revertChange(newValue);
        }
    }

    /// <summary>
    /// Gets property values from schedule.
    /// </summary>
    public (bool isEnabled, string name, string timeText, string hour, string minute, string meridianText, DaysOfWeek daysOfWeek, bool musicEnabled) GetPropertiesFromSchedule(AlarmSchedule? schedule)
    {
        if (schedule == null)
        {
            return (false, string.Empty, string.Empty, "00", "00", "AM", 0, false);
        }

        return (
            isEnabled: schedule.IsEnabled,
            name: schedule.Name ?? string.Empty,
            timeText: schedule.TimeText ?? string.Empty,
            hour: schedule.MeridianHour.ToString("D2"),
            minute: schedule.Minute.ToString("D2"),
            meridianText: schedule.Meridian.ToString().ToUpperInvariant(),
            daysOfWeek: schedule.DaysOfWeek,
            musicEnabled: schedule.MusicEnabled
        );
    }
}
