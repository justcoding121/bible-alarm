#nullable enable
using Bible.Alarm.Common;
using Bible.Alarm.Shared.Helpers;
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
    /// Appends 🎵 to schedule name if music is enabled (or shows just 🎵 if schedule name is empty).
    /// </summary>
    public static string BuildScheduleTitle(ScheduleStateItem scheduleItem)
    {
        if (!scheduleItem.BiblePublicationScheduleId.HasValue)
        {
            var scheduleName = !string.IsNullOrWhiteSpace(scheduleItem.Name)
                ? scheduleItem.Name
                : string.Empty;
            
            // If schedule name is empty and music is enabled, return just the music icon
            if (string.IsNullOrWhiteSpace(scheduleName))
            {
                return scheduleItem.MusicEnabled ? "🎵" : "Unnamed schedule";
            }
            
            // Append music icon to schedule name if music is enabled
            return scheduleItem.MusicEnabled ? scheduleName + " 🎵" : scheduleName;
        }

        var categoryName = scheduleItem.BiblePublicationCategoryName
            ?? JwSourceHelper.GetCategoryName(scheduleItem.BiblePublicationCode ?? string.Empty);
        var isBibleCategory = string.Equals(categoryName, "Bible", StringComparison.OrdinalIgnoreCase);

        string? title = null;
        if (isBibleCategory && !string.IsNullOrWhiteSpace(scheduleItem.BiblePublicationSectionName))
        {
            // For Bible category, show "BookName ChapterNumber" (e.g., "Genesis 1")
            var sectionName = scheduleItem.BiblePublicationSectionName;
            if (!string.IsNullOrWhiteSpace(scheduleItem.BiblePublicationTrackCode))
            {
                title = $"{sectionName} {scheduleItem.BiblePublicationTrackCode}";
            }
            else
            {
                title = sectionName;
            }
        }
        else if (!string.IsNullOrWhiteSpace(scheduleItem.BiblePublicationTrackTitle))
        {
            title = scheduleItem.BiblePublicationTrackTitle;
        }
        else if (!string.IsNullOrWhiteSpace(scheduleItem.BiblePublicationName))
        {
            title = scheduleItem.BiblePublicationName;
        }
        else if (!string.IsNullOrWhiteSpace(scheduleItem.Name))
        {
            title = scheduleItem.Name;
        }

        if (!string.IsNullOrWhiteSpace(title))
        {
            return title;
        }

        // Fallback: if no title found, use schedule name or music icon
        var scheduleName = !string.IsNullOrWhiteSpace(scheduleItem.Name)
            ? scheduleItem.Name
            : string.Empty;
        
        if (string.IsNullOrWhiteSpace(scheduleName))
        {
            return scheduleItem.MusicEnabled ? "🎵" : "Unnamed schedule";
        }
        
        return scheduleItem.MusicEnabled ? scheduleName + " 🎵" : scheduleName;
    }

    /// <summary>
    /// Builds the display subtitle for a ScheduleStateItem.
    /// Format: Schedule Name 🎵 (if music enabled) • Language Name • Publication Name • Section Name (if applicable, excluding Bible category)
    /// If schedule name is empty and music is enabled, shows just 🎵
    /// Bible category: Section names and track names are excluded since they're already shown in the title (e.g., "Leviticus 19").
    /// </summary>
    public static string BuildScheduleSubtitle(ScheduleStateItem scheduleItem)
    {
        if (!scheduleItem.BiblePublicationScheduleId.HasValue)
        {
            var statusText = scheduleItem.IsEnabled ? "Enabled" : "Disabled";
            var timeText = scheduleItem.TimeText;
            return $"• {statusText} • {timeText}";
        }

        var subtitleParts = new List<string>();

        if (!string.IsNullOrWhiteSpace(scheduleItem.Name))
        {
            // Append music icon to schedule name if music is enabled
            var scheduleNameWithMusic = scheduleItem.MusicEnabled 
                ? scheduleItem.Name + " 🎵" 
                : scheduleItem.Name;
            subtitleParts.Add(scheduleNameWithMusic);
        }
        else if (scheduleItem.MusicEnabled)
        {
            // If schedule name is empty but music is enabled, show just the music icon
            subtitleParts.Add("🎵");
        }

        if (!string.IsNullOrWhiteSpace(scheduleItem.BiblePublicationLanguageName))
        {
            subtitleParts.Add(scheduleItem.BiblePublicationLanguageName);
        }

        if (!string.IsNullOrWhiteSpace(scheduleItem.BiblePublicationName))
        {
            subtitleParts.Add(scheduleItem.BiblePublicationName);
        }

        // Skip section name for Bible category since it's already shown in the title (e.g., "Leviticus 19")
        var categoryName = scheduleItem.BiblePublicationCategoryName
            ?? JwSourceHelper.GetCategoryName(scheduleItem.BiblePublicationCode ?? string.Empty);
        var isBibleCategory = string.Equals(categoryName, "Bible", StringComparison.OrdinalIgnoreCase);
        
        var hasSectionStructure = PublicationTypeHelper.HasSectionStructure(scheduleItem.BiblePublicationCode);
        if (hasSectionStructure && !string.IsNullOrWhiteSpace(scheduleItem.BiblePublicationSectionName) && !isBibleCategory)
        {
            subtitleParts.Add(scheduleItem.BiblePublicationSectionName);
        }

        if (subtitleParts.Count > 0)
        {
            return string.Join(" • ", subtitleParts);
        }

        var fallbackStatusText = scheduleItem.IsEnabled ? "Enabled" : "Disabled";
        var fallbackTimeText = scheduleItem.TimeText;
        return $"• {fallbackStatusText} • {fallbackTimeText}";
    }
}