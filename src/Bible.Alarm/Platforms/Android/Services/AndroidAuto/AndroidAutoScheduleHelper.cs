#nullable enable
using Bible.Alarm.Common;
using Bible.Alarm.Services.Schedule;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Fluxor;
using Serilog;

namespace Bible.Alarm.Platforms.Android.Services.AndroidAuto;

/// <summary>
/// Helper service for shared Android Auto schedule display logic.
/// Provides methods for loading schedules from Fluxor state and formatting display information.
/// </summary>
public static class AndroidAutoScheduleHelper
{
    private static readonly ILogger logger = Log.ForContext(typeof(AndroidAutoScheduleHelper));

    /// <summary>
    /// Loads schedule state items from Fluxor ApplicationState.
    /// Returns empty list if state is not initialized or no schedules are available.
    /// </summary>
    public static List<ScheduleStateItem> LoadScheduleStateItemsFromState()
    {
        try
        {
            logger.Debug("Loading schedules from state for Android Auto");

            // Get state from service provider - schedules are already loaded during bootstrap
            var state = ServiceProviderManager.GetService<IState<ApplicationState>>();

            if (state?.Value?.Schedules != null && state.Value.Schedules.Count > 0)
            {
                // Return ScheduleStateItem list which includes BiblePublicationLanguageName
                var scheduleItems = state.Value.Schedules.ToList();
                logger.Information("Loaded {Count} schedules from state for Android Auto", scheduleItems.Count);
                return scheduleItems;
            }

            logger.Warning("No schedules found in state - state may not be initialized yet");
            return [];
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error loading schedules from state for Android Auto");
            return [];
        }
    }

    /// <summary>
    /// Builds the display title for a schedule state item.
    /// Bible category: section name (e.g. "Hebrews 13"). Other categories: track name.
    /// When this schedule is currently playing, uses PlaybackState.Title so Auto and phone list show the actual track.
    /// Appends 🎵 to schedule name if music is enabled (or shows just 🎵 if schedule name is empty).
    /// </summary>
    public static string BuildScheduleTitle(ScheduleStateItem scheduleItem)
    {
        var playbackState = ServiceProviderManager.GetService<IState<PlaybackState>>()?.Value;
        var usePlayingTitle = playbackState != null
            && playbackState.CurrentScheduleId == scheduleItem.Id
            && playbackState.IsPreparingOrPlaying
            && !string.IsNullOrWhiteSpace(playbackState.Title);

        if (usePlayingTitle)
        {
            return playbackState!.Title!;
        }

        return ScheduleDisplayMetadataHelper.BuildScheduleTitle(scheduleItem);
    }

    /// <summary>
    /// Builds the display subtitle for a ScheduleStateItem.
    /// Format: Schedule Name 🎵 (if music enabled) • Language Name • Publication Name • Section Name (if applicable, excluding Bible category)
    /// If schedule name is empty and music is enabled, shows just 🎵
    /// Bible category: Section names and track names are excluded since they're already shown in the title (e.g., "Leviticus 19").
    /// </summary>
    public static string BuildScheduleSubtitle(ScheduleStateItem scheduleItem) =>
        ScheduleDisplayMetadataHelper.BuildScheduleSubtitle(scheduleItem);
}

