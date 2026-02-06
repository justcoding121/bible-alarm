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
    /// Bible category: section name (e.g. "Hebrews 13"). Other categories: track name.
    /// When this schedule is currently playing, uses PlaybackState.Title so Auto and phone list show the actual track.
    /// </summary>
    public static string BuildScheduleTitle(ScheduleStateItem scheduleItem)
    {
        if (!scheduleItem.BiblePublicationScheduleId.HasValue)
        {
            return !string.IsNullOrWhiteSpace(scheduleItem.Name)
                ? scheduleItem.Name
                : "Unnamed schedule";
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
            return scheduleItem.BiblePublicationSectionName;
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

        return !string.IsNullOrWhiteSpace(scheduleItem.Name)
            ? scheduleItem.Name
            : "Unnamed schedule";
    }

    /// <summary>
    /// Builds the display subtitle for a ScheduleStateItem.
    /// Format: Schedule Name (if not empty) • Publication Name • Section Name (if applicable) • Language Name • 🎵 (if music enabled)
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
            subtitleParts.Add(scheduleItem.Name);
        }

        if (!string.IsNullOrWhiteSpace(scheduleItem.BiblePublicationName))
        {
            subtitleParts.Add(scheduleItem.BiblePublicationName);
        }

        var hasSectionStructure = PublicationTypeHelper.HasSectionStructure(scheduleItem.BiblePublicationCode);
        if (hasSectionStructure && !string.IsNullOrWhiteSpace(scheduleItem.BiblePublicationSectionName))
        {
            subtitleParts.Add(scheduleItem.BiblePublicationSectionName);
        }

        if (!string.IsNullOrWhiteSpace(scheduleItem.BiblePublicationLanguageName))
        {
            subtitleParts.Add(scheduleItem.BiblePublicationLanguageName);
        }

        if (scheduleItem.MusicEnabled)
        {
            subtitleParts.Add("🎵");
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

