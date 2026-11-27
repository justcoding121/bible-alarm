#nullable enable
using Bible.Alarm.Common;
using Bible.Alarm.Services.Media.BrowseHelpers;
using Bible.Alarm.Services.Schedule;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Fluxor;
using Serilog;

namespace Bible.Alarm.Platforms.Android.Services.AndroidAuto;

/// <summary>
/// Helper service for shared Android Auto schedule display logic.
/// Provides methods for loading schedules from Fluxor state and formatting display information.
/// Uses Beamed Eighth Notes (U+266B) for music indicator instead of emoji for better car display compatibility.
/// </summary>
public static class AndroidAutoScheduleHelper
{
    private const string MusicSymbol = "\u266B";

    private static readonly ILogger logger = Log.ForContext(typeof(AndroidAutoScheduleHelper));

    /// <summary>
    /// Loads schedule state items from Fluxor ApplicationState.
    /// Returns empty list if state is not initialized or no schedules are available.
    /// </summary>
    /// <summary>
    /// Schedules for the car browse tree. Uses Fluxor when bootstrap has filled it,
    /// and the schedule database when the car host bound the process before Fluxor was ready.
    /// </summary>
    public static List<ScheduleStateItem> LoadScheduleStateItems()
    {
        var fromState = LoadScheduleStateItemsFromState();
        var chosen = ScheduleBrowseSelector.Select(fromState, fromState.Count > 0 ? fromState : LoadFromDatabase());
        return chosen.ToList();
    }

    public static List<ScheduleStateItem> LoadScheduleStateItemsFromState()
    {
        try
        {
            logger.Debug(AppConstants.Logging.AndroidAutoScheduleHelperDiagnosticsLog.LoadingSchedulesFromStateForAa);

            // Schedules are already loaded during bootstrap
            var state = ServiceProviderManager.GetService<IState<ApplicationState>>();

            if (state?.Value?.Schedules != null && state.Value.Schedules.Count > 0)
            {
                var scheduleItems = state.Value.Schedules.ToList();
                logger.Information("Loaded {Count} schedules from state for Android Auto", scheduleItems.Count);
                return scheduleItems;
            }

            logger.Warning(AppConstants.Logging.AndroidAutoScheduleHelperDiagnosticsLog.NoSchedulesFoundInStateMayNotBeInitialized);
            return [];
        }
        catch (Exception ex)
        {
            logger.Error(ex, AppConstants.Logging.AndroidAutoScheduleHelperDiagnosticsLog.ErrorLoadingSchedulesFromStateForAa);
            return [];
        }
    }

    private static List<ScheduleStateItem> LoadFromDatabase()
    {
        try
        {
            if (!MauiAppHolder.IsInitialized)
            {
                logger.Warning("Android Auto browse skipped the schedule database because the app container is not created yet");
                return [];
            }

            var service = ServiceProviderManager.GetService<IAlarmScheduleService>();
            var schedules = service.GetAllSchedulesAsync().GetAwaiter().GetResult();
            logger.Information("Loaded {Count} schedules from the database for Android Auto", schedules.Count);
            return schedules.Select(MapSchedule).ToList();
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to load schedules from the database for Android Auto");
            return [];
        }
    }

    private static ScheduleStateItem MapSchedule(AlarmSchedule schedule) => new()
    {
        Id = schedule.Id,
        Name = schedule.Name,
        IsEnabled = schedule.IsEnabled,
        Hour = schedule.Hour,
        Minute = schedule.Minute,
        Second = schedule.Second,
        DaysOfWeek = schedule.DaysOfWeek,
        NotificationEnabled = schedule.NotificationEnabled,
        MusicEnabled = schedule.MusicEnabled,
        SnoozeMinutes = schedule.SnoozeMinutes,
        NumberOfTracksToPlay = schedule.NumberOfTracksToPlay,
        AlwaysPlayFromStart = schedule.AlwaysPlayFromStart
    };

    /// <summary>
    /// Builds the display title for a schedule state item.
    /// Bible category: section name (e.g. "Hebrews 13"). Other categories: track name.
    /// Always uses schedule metadata (Bible pub details) so the listing matches the home page, even when music is enabled and playing.
    /// </summary>
    public static string BuildScheduleTitle(ScheduleStateItem scheduleItem) =>
        ScheduleDisplayMetadataHelper.BuildScheduleTitle(scheduleItem, MusicSymbol);

    /// <summary>
    /// Builds the display subtitle for a ScheduleStateItem.
    /// Format: Schedule Name • Category • Language Name • Publication Name • Section Name (if applicable, excluding Bible category)
    /// Music icon is shown in the title, not the subtitle.
    /// </summary>
    public static string BuildScheduleSubtitle(ScheduleStateItem scheduleItem) =>
        ScheduleDisplayMetadataHelper.BuildScheduleSubtitle(scheduleItem, MusicSymbol);
}

