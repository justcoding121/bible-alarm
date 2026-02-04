#nullable enable
using Bible.Alarm.Common;
using Bible.Alarm.Shared.Helpers;
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
    /// Title is the Publication Name.
    /// </summary>
    public static string BuildScheduleTitle(ScheduleStateItem scheduleItem)
    {
        if (scheduleItem.BiblePublicationScheduleId.HasValue)
        {
            // Title is the Publication Name
            if (!string.IsNullOrWhiteSpace(scheduleItem.BiblePublicationName))
            {
                return scheduleItem.BiblePublicationName;
            }
        }

        // Fallback: return schedule name if no Bible reading schedule
        return !string.IsNullOrWhiteSpace(scheduleItem.Name)
            ? scheduleItem.Name
            : "Unnamed schedule";
    }

    /// <summary>
    /// Builds the display subtitle for a ScheduleStateItem.
    /// Format: • Schedule Name (if not empty) • Section Name (if exists) • Track title • Language (if languaged pub) 🎵 (if music enabled)
    /// </summary>
    public static string BuildScheduleSubtitle(ScheduleStateItem scheduleItem)
    {
        if (!scheduleItem.BiblePublicationScheduleId.HasValue)
        {
            // Fallback: show status and time if no Bible reading schedule
            var statusText = scheduleItem.IsEnabled ? "Enabled" : "Disabled";
            var timeText = scheduleItem.TimeText;
            return $"• {statusText} • {timeText}";
        }

        var subtitleParts = new List<string>();

        // Add Schedule Name (if not empty)
        if (!string.IsNullOrWhiteSpace(scheduleItem.Name))
        {
            subtitleParts.Add(scheduleItem.Name);
        }

        // Add Section/Track info
        var hasSectionStructure = PublicationTypeHelper.HasSectionStructure(scheduleItem.BiblePublicationCode);

        if (hasSectionStructure)
        {
            // For sectioned publications:
            // - Music category: use track title (e.g., "Melody Number(s) 195, 224")
            // - Other categories (Bible, etc.): use track number (e.g., "9")
            // Combine section + track into single part: "Genesis 1"
            var categoryName = scheduleItem.BiblePublicationCategoryName
                ?? JwSourceHelper.GetCategoryName(scheduleItem.BiblePublicationCode ?? string.Empty);
            var isMusicCategory = string.Equals(categoryName, "Music", StringComparison.OrdinalIgnoreCase);

            var sectionName = scheduleItem.BiblePublicationSectionName;
            string? trackPart = null;

            if (isMusicCategory && !string.IsNullOrWhiteSpace(scheduleItem.BiblePublicationTrackTitle))
            {
                trackPart = scheduleItem.BiblePublicationTrackTitle;
            }
            else
            {
                trackPart = scheduleItem.BiblePublicationTrackNumber.HasValue && scheduleItem.BiblePublicationTrackNumber.Value > 0
                    ? scheduleItem.BiblePublicationTrackNumber.Value.ToString()
                    : null;
            }

            // Combine section + track into one part (e.g., "Genesis 1")
            if (!string.IsNullOrWhiteSpace(sectionName) && !string.IsNullOrWhiteSpace(trackPart))
            {
                subtitleParts.Add($"{sectionName} {trackPart}");
            }
            else if (!string.IsNullOrWhiteSpace(sectionName))
            {
                subtitleParts.Add(sectionName);
            }
            else if (!string.IsNullOrWhiteSpace(trackPart))
            {
                subtitleParts.Add(trackPart);
            }
        }
        else
        {
            // Non-sectioned publications (dramas/videos): show track title
            if (!string.IsNullOrWhiteSpace(scheduleItem.BiblePublicationTrackTitle))
            {
                subtitleParts.Add(scheduleItem.BiblePublicationTrackTitle);
            }
        }

        // Add Language only if BiblePublicationLanguageName is set.
        // Publications without language (like "iam" instrumental music) don't have a language name in the media index.
        // Don't fall back to BiblePublicationLanguageCode as it might be stale from cascade logic.
        if (!string.IsNullOrWhiteSpace(scheduleItem.BiblePublicationLanguageName))
        {
            subtitleParts.Add(scheduleItem.BiblePublicationLanguageName);
        }

        // Add music note emoji if music is enabled (at the end)
        if (scheduleItem.MusicEnabled)
        {
            subtitleParts.Add("🎵");
        }

        if (subtitleParts.Count > 0)
        {
            return string.Join(" • ", subtitleParts);
        }

        // Fallback: show status and time
        var fallbackStatusText = scheduleItem.IsEnabled ? "Enabled" : "Disabled";
        var fallbackTimeText = scheduleItem.TimeText;
        return $"• {fallbackStatusText} • {fallbackTimeText}";
    }
}

