#nullable enable
using Bible.Alarm.Common;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Fluxor;
using Serilog;

namespace Bible.Alarm.Platforms.iOS.Services.CarPlay;

/// <summary>
/// Helper service for CarPlay schedule display logic.
/// Provides methods for loading schedules from Fluxor state and formatting display information.
/// </summary>
public static class CarPlayScheduleHelper
{
    private static readonly ILogger logger = Log.ForContext(typeof(CarPlayScheduleHelper));

    /// <summary>
    /// Loads schedule state items from Fluxor ApplicationState.
    /// Returns empty list if state is not initialized or no schedules are available.
    /// </summary>
    public static List<ScheduleStateItem> LoadScheduleStateItemsFromState()
    {
        try
        {
            logger.Debug("[CarPlay] Loading schedules from state");

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
    /// Format: BookName Chapter Number (e.g., "Joshua 22")
    /// </summary>
    public static string BuildScheduleTitle(ScheduleStateItem scheduleItem)
    {
        if (scheduleItem.BibleReadingScheduleId.HasValue)
        {
            var titleParts = new List<string>();

            // Add book name or fallback to book number
            if (!string.IsNullOrWhiteSpace(scheduleItem.BibleReadingBookName))
            {
                titleParts.Add(scheduleItem.BibleReadingBookName);
            }
            else if (scheduleItem.BibleReadingBookNumber.HasValue && scheduleItem.BibleReadingBookNumber.Value > 0)
            {
                titleParts.Add($"Book {scheduleItem.BibleReadingBookNumber.Value}");
            }

            // Add chapter number
            if (scheduleItem.BibleReadingChapterNumber.HasValue && scheduleItem.BibleReadingChapterNumber.Value > 0)
            {
                titleParts.Add(scheduleItem.BibleReadingChapterNumber.Value.ToString());
            }

            if (titleParts.Count > 0)
            {
                return string.Join(" ", titleParts);
            }
        }

        // Fallback: return schedule name
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
            var languageName = !string.IsNullOrWhiteSpace(scheduleItem.BibleReadingLanguageName)
                ? scheduleItem.BibleReadingLanguageName
                : scheduleItem.BibleReadingLanguageCode ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(languageName))
            {
                // Add music note emoji if music is enabled
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

        // Fallback: show status and time
        var statusText = scheduleItem.IsEnabled ? "Enabled" : "Disabled";
        var timeText = scheduleItem.TimeText;
        return $"{statusText} • {timeText}";
    }
}
