#nullable enable
using Bible.Alarm.Common;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Services.Media.Interfaces;
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

        var playbackState = ServiceProviderManager.GetService<IState<PlaybackState>>()?.Value;
        var usePlayingTitle = playbackState != null
            && playbackState.CurrentScheduleId == scheduleItem.Id
            && playbackState.IsPreparingOrPlaying
            && !string.IsNullOrWhiteSpace(playbackState.Title);

        var categoryName = scheduleItem.BiblePublicationCategoryName
            ?? JwSourceHelper.GetCategoryName(scheduleItem.BiblePublicationCode ?? string.Empty);
        var isBibleCategory = string.Equals(categoryName, "Bible", StringComparison.OrdinalIgnoreCase);

        if (isBibleCategory && !string.IsNullOrWhiteSpace(scheduleItem.BiblePublicationSectionName))
        {
            // For Bible category, show "BookName ChapterNumber" (e.g., "Genesis 1")
            var sectionName = scheduleItem.BiblePublicationSectionName;
            if (!string.IsNullOrWhiteSpace(scheduleItem.BiblePublicationTrackCode))
            {
                return $"{sectionName} {scheduleItem.BiblePublicationTrackCode}";
            }
            return sectionName;
        }

        if (usePlayingTitle)
        {
            return playbackState!.Title!;
        }

        if (!string.IsNullOrWhiteSpace(scheduleItem.BiblePublicationTrackTitle))
        {
            return scheduleItem.BiblePublicationTrackTitle;
        }

        if (!string.IsNullOrWhiteSpace(scheduleItem.BiblePublicationName))
        {
            return scheduleItem.BiblePublicationName;
        }

        var fallbackName = !string.IsNullOrWhiteSpace(scheduleItem.Name)
            ? scheduleItem.Name
            : "Unnamed schedule";
        
        // Append music icon to schedule name if music is enabled
        if (scheduleItem.MusicEnabled)
        {
            return fallbackName + " 🎵";
        }
        
        return fallbackName;
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

        var categoryCode = scheduleItem.BiblePublicationCategoryName
            ?? JwSourceHelper.GetCategoryName(scheduleItem.BiblePublicationCode ?? string.Empty);

        if (!string.IsNullOrWhiteSpace(categoryCode))
        {
            var categoryDisplayName = ResolveCategoryDisplayName(categoryCode);
            var scheduleName = scheduleItem.Name?.Trim() ?? string.Empty;
            if (!string.Equals(categoryDisplayName.Trim(), scheduleName, StringComparison.OrdinalIgnoreCase))
            {
                subtitleParts.Add(categoryDisplayName);
            }
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
        var isBibleCategory = string.Equals(categoryCode, "Bible", StringComparison.OrdinalIgnoreCase);
        
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

    private static string ResolveCategoryDisplayName(string categoryCode)
    {
        try
        {
            var categoryNameService = ServiceProviderManager.GetService<ICategoryNameService>();
            return categoryNameService?.GetName(categoryCode, AppConstants.Media.DefaultLanguageCode)
                ?? categoryCode;
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to resolve category display name for code {CategoryCode}", categoryCode);
            return categoryCode;
        }
    }
}

