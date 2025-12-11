#nullable enable
using Bible.Alarm.Common;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Stores;
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
    /// Loads schedules from Fluxor ApplicationState.
    /// Returns empty list if state is not initialized or no schedules are available.
    /// </summary>
    public static List<AlarmSchedule> LoadSchedulesFromState()
    {
        try
        {
            Logger.Debug("Loading schedules from state for Android Auto");
            
            // Get state from service provider - schedules are already loaded during bootstrap
            var state = ServiceProviderManager.GetService<IState<ApplicationState>>();
            
            if (state?.Value?.Schedules != null && state.Value.Schedules.Count > 0)
            {
                // Convert ObservableHashSet to List for easier iteration
                // Translation names are already populated in the state during bootstrap
                var schedules = state.Value.Schedules.ToList();
                Logger.Information("Loaded {Count} schedules from state for Android Auto", schedules.Count);
                return schedules;
            }
            else
            {
                Logger.Warning("No schedules found in state - state may not be initialized yet");
                return new List<AlarmSchedule>();
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error loading schedules from state for Android Auto");
            return new List<AlarmSchedule>();
        }
    }

    /// <summary>
    /// Builds the display title for a schedule.
    /// Returns schedule name if not empty, otherwise "Schedule {Id}".
    /// </summary>
    public static string BuildScheduleTitle(AlarmSchedule schedule)
    {
        return !string.IsNullOrWhiteSpace(schedule.Name) 
            ? schedule.Name 
            : $"Schedule {schedule.Id}";
    }

    /// <summary>
    /// Builds the display subtitle for a schedule.
    /// Returns formatted subtitle with Language, Book, Chapter if BibleReadingSchedule exists,
    /// otherwise returns status and time.
    /// </summary>
    public static string BuildScheduleSubtitle(AlarmSchedule schedule)
    {
        var subtitleParts = new List<string>();
        
        if (schedule.BibleReadingSchedule != null)
        {
            var bibleReading = schedule.BibleReadingSchedule;
            
            // Use TranslationName from state (populated during bootstrap)
            // Fallback to LanguageCode if TranslationName is not set
            var languageName = !string.IsNullOrWhiteSpace(bibleReading.TranslationName)
                ? bibleReading.TranslationName
                : bibleReading.LanguageCode;
            
            // Build subtitle with Language, Book Number, Chapter Number
            // Use data directly from schedule to avoid any async calls that could block
            if (!string.IsNullOrWhiteSpace(languageName))
            {
                subtitleParts.Add(languageName);
            }
            
            // Add book number (we skip book name lookup to avoid async calls)
            // Book number is sufficient for identification
            if (bibleReading.BookNumber > 0)
            {
                subtitleParts.Add($"Book {bibleReading.BookNumber}");
            }
            
            // Add chapter number
            if (bibleReading.ChapterNumber > 0)
            {
                subtitleParts.Add($"Chapter {bibleReading.ChapterNumber}");
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
            var statusText = schedule.IsEnabled ? "Enabled" : "Disabled";
            var timeText = schedule.TimeText;
            return $"{statusText} • {timeText}";
        }
    }
}

