#nullable enable
using Bible.Alarm.Common;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Services.Schedule;
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
    public static List<ScheduleStateItem> LoadScheduleStateItemsFromState()
    {
        try
        {
            logger.Debug(AppConstants.Logging.AndroidAutoScheduleHelperDiagnosticsLog.LoadingSchedulesFromStateForAa);

            // Get state from service provider - schedules are already loaded during bootstrap
            var state = ServiceProviderManager.GetService<IState<ApplicationState>>();

            if (state?.Value?.Schedules != null && state.Value.Schedules.Count > 0)
            {
                // Return ScheduleStateItem list which includes BiblePublicationLanguageName
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

