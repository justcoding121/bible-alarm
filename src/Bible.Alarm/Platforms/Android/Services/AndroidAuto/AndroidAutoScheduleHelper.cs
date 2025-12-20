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
                // Return ScheduleStateItem list which includes TranslationName
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
    /// Format: BookName Chapter Number (e.g., "Joshua 22")
    /// </summary>
    public static string BuildScheduleTitle(ScheduleStateItem scheduleItem)
    {
        if (scheduleItem.BibleReadingScheduleId.HasValue)
        {
            // Build title as "BookName ChapterNumber" (e.g., "Joshua 22")
            var titleParts = new List<string>();

            // Add book name (populated during bootstrap) or fallback to book number
            if (!string.IsNullOrWhiteSpace(scheduleItem.BookName))
            {
                titleParts.Add(scheduleItem.BookName);
            }
            else if (scheduleItem.BibleReadingBookNumber.HasValue && scheduleItem.BibleReadingBookNumber.Value > 0)
            {
                // Fallback to book number if book name is not available
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
            // Use TranslationName from ScheduleStateItem (populated during bootstrap)
            // Fallback to LanguageCode if TranslationName is not set
            var languageName = !string.IsNullOrWhiteSpace(scheduleItem.TranslationName)
                ? scheduleItem.TranslationName
                : scheduleItem.BibleReadingLanguageCode ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(languageName))
            {
                subtitleParts.Add(languageName);
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

