#nullable enable
using Bible.Alarm.Common;
using Bible.Alarm.Services.Schedule;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Fluxor;
using Serilog;

namespace Bible.Alarm.Platforms.iOS.Services.CarPlay;

/// <summary>
/// Helper service for CarPlay schedule display logic.
/// Provides methods for loading schedules from Fluxor state and formatting display information.
/// Uses Beamed Eighth Notes (U+266B) for music indicator instead of emoji for better car display compatibility.
/// </summary>
public static class CarPlayScheduleHelper
{
    private const string MusicSymbol = "\u266B";

    private static readonly ILogger logger = Log.ForContext(typeof(CarPlayScheduleHelper));

    /// <summary>
    /// Loads schedule state items from Fluxor ApplicationState.
    /// Returns empty list if state is not initialized or no schedules are available.
    /// </summary>
    public static List<ScheduleStateItem> LoadScheduleStateItemsFromState()
    {
        try
        {
            // Get state from service provider - schedules are already loaded during bootstrap
            var state = ServiceProviderManager.GetService<IState<ApplicationState>>();

            if (state?.Value?.Schedules != null && state.Value.Schedules.Count > 0)
            {
                var scheduleItems = state.Value.Schedules.ToList();
                logger.Information("[CarPlay] Loaded {Count} schedules from state", scheduleItems.Count);
                return scheduleItems;
            }

            logger.Warning("[CarPlay] No schedules found in state - state may not be initialized yet");
            return [];
        }
        catch (Exception ex)
        {
            logger.Error(ex, "[CarPlay] Error loading schedules from state");
            return [];
        }
    }

    /// <summary>
    /// Builds the display title for a schedule state item.
    /// Bible category: section name (e.g. "Hebrews 13"). Other categories: track name.
    /// Appends ♫ to schedule name if music is enabled (or shows just ♫ if schedule name is empty).
    /// </summary>
    public static string BuildScheduleTitle(ScheduleStateItem scheduleItem) =>
        ScheduleDisplayMetadataHelper.BuildScheduleTitle(scheduleItem, MusicSymbol);

    /// <summary>
    /// Builds the display subtitle for a ScheduleStateItem.
    /// Format: Schedule Name ♫ (if music enabled) • Language Name • Publication Name • Section Name (if applicable, excluding Bible category)
    /// If schedule name is empty and music is enabled, shows just ♫
    /// Bible category: Section names and track names are excluded since they're already shown in the title (e.g., "Leviticus 19").
    /// </summary>
    public static string BuildScheduleSubtitle(ScheduleStateItem scheduleItem) =>
        ScheduleDisplayMetadataHelper.BuildScheduleSubtitle(scheduleItem, MusicSymbol);
}