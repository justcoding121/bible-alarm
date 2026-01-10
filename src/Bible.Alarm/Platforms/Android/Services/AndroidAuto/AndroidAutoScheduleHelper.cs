#nullable enable
using Bible.Alarm.Common;
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
                // Return ScheduleStateItem list which includes BibleReadingLanguageName
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
    /// Format: SectionName Track Number (e.g., "Joshua 22")
    /// </summary>
    public static string BuildScheduleTitle(ScheduleStateItem scheduleItem)
    {
        if (scheduleItem.BibleReadingScheduleId.HasValue)
        {
            // Build title as "SectionName TrackNumber" (e.g., "Joshua 22")
            var titleParts = new List<string>();

            // Add section name (populated during bootstrap) or fallback to section number
            if (!string.IsNullOrWhiteSpace(scheduleItem.BibleReadingSectionName))
            {
                titleParts.Add(scheduleItem.BibleReadingSectionName);
            }
            else if (scheduleItem.BibleReadingSectionNumber.HasValue && scheduleItem.BibleReadingSectionNumber.Value > 0)
            {
                // Fallback to section number if section name is not available
                titleParts.Add($"Section {scheduleItem.BibleReadingSectionNumber.Value}");
            }

            // Add track number
            if (scheduleItem.BibleReadingTrackNumber.HasValue && scheduleItem.BibleReadingTrackNumber.Value > 0)
            {
                titleParts.Add(scheduleItem.BibleReadingTrackNumber.Value.ToString());
            }

            if (titleParts.Count > 0)
            {
                return string.Join(" ", titleParts);
            }
        }

        // Fallback: return schedule name if no Bible reading schedule
        return !string.IsNullOrWhiteSpace(scheduleItem.Name)
            ? scheduleItem.Name
            : "Unnamed schedule";
    }

    /// <summary>
    /// Builds the display subtitle for a ScheduleStateItem.
    /// Format: • Schedule Name • Language (schedule name omitted if empty)
    /// </summary>
    public static string BuildScheduleSubtitle(ScheduleStateItem scheduleItem)
    {
        var subtitleParts = new List<string>();

        // Add schedule name with bullet points only if not empty
        if (!string.IsNullOrWhiteSpace(scheduleItem.Name))
        {
            subtitleParts.Add($"• {scheduleItem.Name} •");
        }

        // Add language if Bible reading schedule exists
        if (scheduleItem.BibleReadingScheduleId.HasValue)
        {
            // Use BibleReadingLanguageName from ScheduleStateItem (populated during bootstrap)
            // Fallback to LanguageCode if BibleReadingLanguageName is not set
            var languageName = !string.IsNullOrWhiteSpace(scheduleItem.BibleReadingLanguageName)
                ? scheduleItem.BibleReadingLanguageName
                : scheduleItem.BibleReadingLanguageCode ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(languageName))
            {
                // Add music note emoji (🎵) to the right of language text if music is enabled
                // Using emoji instead of single note symbol for larger, more visible appearance
                var languageText = scheduleItem.MusicEnabled
                    ? $"{languageName} 🎵"
                    : languageName;
                subtitleParts.Add(languageText);
            }
        }

        if (subtitleParts.Count > 0)
        {
            return string.Join(" ", subtitleParts);
        }

        // Fallback: show status and time if no Bible reading schedule
        var statusText = scheduleItem.IsEnabled ? "Enabled" : "Disabled";
        var timeText = scheduleItem.TimeText;
        return $"{statusText} • {timeText}";
    }
}

