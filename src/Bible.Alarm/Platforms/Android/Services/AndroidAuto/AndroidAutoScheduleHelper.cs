#nullable enable
using Bible.Alarm.Common;
using Bible.Alarm.Models.Schedule;
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
    private static readonly ILogger Logger = Log.ForContext(typeof(AndroidAutoScheduleHelper));

    /// <summary>
    /// Loads schedule state items from Fluxor ApplicationState.
    /// Returns empty list if state is not initialized or no schedules are available.
    /// </summary>
    public static List<ScheduleStateItem> LoadScheduleStateItemsFromState()
    {
        try
        {
            Logger.Debug("Loading schedules from state for Android Auto");
            
            // Get state from service provider - schedules are already loaded during bootstrap
            var state = ServiceProviderManager.GetService<IState<ApplicationState>>();
            
            if (state?.Value?.Schedules != null && state.Value.Schedules.Count > 0)
            {
                // Return ScheduleStateItem list which includes TranslationName
                var scheduleItems = state.Value.Schedules.ToList();
                Logger.Information("Loaded {Count} schedules from state for Android Auto", scheduleItems.Count);
                return scheduleItems;
            }
            else
            {
                Logger.Warning("No schedules found in state - state may not be initialized yet");
                return new List<ScheduleStateItem>();
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error loading schedules from state for Android Auto");
            return new List<ScheduleStateItem>();
        }
    }
    
    /// <summary>
    /// Builds the display title for a schedule state item.
    /// Returns schedule name if not empty, otherwise "Unnamed schedule".
    /// </summary>
    public static string BuildScheduleTitle(ScheduleStateItem scheduleItem)
    {
        return !string.IsNullOrWhiteSpace(scheduleItem.Name) 
            ? scheduleItem.Name 
            : "Unnamed schedule";
    }

    /// <summary>
    /// Builds the display subtitle for a ScheduleStateItem.
    /// Uses TranslationName from the ScheduleStateItem.
    /// Returns formatted subtitle with Language, Book, Chapter if BibleReadingSchedule exists,
    /// otherwise returns status and time.
    /// </summary>
    public static string BuildScheduleSubtitle(ScheduleStateItem scheduleItem)
    {
        var subtitleParts = new List<string>();
        
        if (scheduleItem.BibleReadingScheduleId.HasValue)
        {
            // Use TranslationName from ScheduleStateItem (populated during bootstrap)
            // Fallback to LanguageCode if TranslationName is not set
            var languageName = !string.IsNullOrWhiteSpace(scheduleItem.TranslationName)
                ? scheduleItem.TranslationName
                : scheduleItem.BibleReadingLanguageCode ?? string.Empty;
            
            // Build subtitle with Language, Book Number, Chapter Number
            // Use data directly from DTO to avoid any async calls that could block
            if (!string.IsNullOrWhiteSpace(languageName))
            {
                subtitleParts.Add(languageName);
            }
            
            // Add book name (populated during bootstrap) or fallback to book number
            if (!string.IsNullOrWhiteSpace(scheduleItem.BookName))
            {
                subtitleParts.Add(scheduleItem.BookName);
            }
            else if (scheduleItem.BibleReadingBookNumber.HasValue && scheduleItem.BibleReadingBookNumber.Value > 0)
            {
                // Fallback to book number if book name is not available
                subtitleParts.Add($"Book {scheduleItem.BibleReadingBookNumber.Value}");
            }
            
            // Add chapter number
            if (scheduleItem.BibleReadingChapterNumber.HasValue && scheduleItem.BibleReadingChapterNumber.Value > 0)
            {
                subtitleParts.Add($"Chapter {scheduleItem.BibleReadingChapterNumber.Value}");
            }
        }
        
        // Set subtitle - Language, Book, Chapter (or status/time if no Bible reading)
        if (subtitleParts.Count > 0)
        {
            return string.Join(" • ", subtitleParts);
        }
        else
        {
            // Fallback: show status and time if no Bible reading schedule
            var statusText = scheduleItem.IsEnabled ? "Enabled" : "Disabled";
            var timeText = scheduleItem.TimeText;
            return $"{statusText} • {timeText}";
        }
    }
}

