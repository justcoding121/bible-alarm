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
    /// Format for sectioned Bible: "Section Name - Track Number" (e.g., "Genesis - 1")
    /// Format for drama/video: Track Title (e.g., "Adam and Eve in the Garden of Eden")
    /// </summary>
    public static string BuildScheduleTitle(ScheduleStateItem scheduleItem)
    {
        if (scheduleItem.BiblePublicationScheduleId.HasValue)
        {
            var hasSectionStructure = PublicationTypeHelper.HasSectionStructure(scheduleItem.BiblePublicationCode);

            if (hasSectionStructure)
            {
                // Traditional Bible: "Section Name - Track Number" (e.g., "Genesis - 1")
                var sectionName = !string.IsNullOrWhiteSpace(scheduleItem.BiblePublicationSectionName)
                    ? scheduleItem.BiblePublicationSectionName
                    : null;

                var trackNumber = scheduleItem.BiblePublicationTrackNumber.HasValue && scheduleItem.BiblePublicationTrackNumber.Value > 0
                    ? scheduleItem.BiblePublicationTrackNumber.Value.ToString()
                    : null;

                string? title = null;
                if (sectionName != null && trackNumber != null)
                {
                    title = $"{sectionName} {trackNumber}";
                }
                else if (sectionName != null)
                {
                    title = sectionName;
                }
                else if (trackNumber != null)
                {
                    title = trackNumber;
                }

                if (title != null)
                {
                    return title;
                }
            }
            else
            {
                // Drama/Video: Show track title
                if (!string.IsNullOrWhiteSpace(scheduleItem.BiblePublicationTrackTitle))
                {
                    return scheduleItem.BiblePublicationTrackTitle;
                }
            }
        }

        // Fallback: return schedule name if no Bible reading schedule
        return !string.IsNullOrWhiteSpace(scheduleItem.Name)
            ? scheduleItem.Name
            : "Unnamed schedule";
    }

    /// <summary>
    /// Builds the display subtitle for a ScheduleStateItem.
    /// Format: * Publication Name * Section Name * Track Name * Language (+ 🎵 if music enabled)
    /// For Bible category, section name and track number are combined (e.g. "* Exodus 9") instead of separate section/track parts.
    /// </summary>
    public static string BuildScheduleSubtitle(ScheduleStateItem scheduleItem)
    {
        if (!scheduleItem.BiblePublicationScheduleId.HasValue)
        {
            // Fallback: show status and time if no Bible reading schedule
            var statusText = scheduleItem.IsEnabled ? "Enabled" : "Disabled";
            var timeText = scheduleItem.TimeText;
            return $"* {statusText} * {timeText}";
        }

        var subtitleParts = new List<string>();

        // Add Publication Name
        if (!string.IsNullOrWhiteSpace(scheduleItem.BiblePublicationName))
        {
            subtitleParts.Add($"* {scheduleItem.BiblePublicationName}");
        }

        var isBibleCategory = string.Equals(scheduleItem.BiblePublicationCategoryName, "Bible", StringComparison.OrdinalIgnoreCase);

        // Add Section/Track info
        var hasSectionStructure = PublicationTypeHelper.HasSectionStructure(scheduleItem.BiblePublicationCode);

        if (isBibleCategory && hasSectionStructure)
        {
            // Bible category: combine section name + track number (e.g. "Exodus 9")
            var sectionName = scheduleItem.BiblePublicationSectionName;
            var trackNumber = scheduleItem.BiblePublicationTrackNumber.HasValue && scheduleItem.BiblePublicationTrackNumber.Value > 0
                ? scheduleItem.BiblePublicationTrackNumber.Value.ToString()
                : null;

            if (!string.IsNullOrWhiteSpace(sectionName) && trackNumber != null)
            {
                subtitleParts.Add($"* {sectionName} {trackNumber}");
            }
            else if (!string.IsNullOrWhiteSpace(sectionName))
            {
                subtitleParts.Add($"* {sectionName}");
            }
            else if (trackNumber != null)
            {
                subtitleParts.Add($"* {trackNumber}");
            }
        }
        else
        {
            // Non-Bible categories:
            // - If sectioned, show section name + track title (fallback to track number).
            // - If non-sectioned, show track title.
            if (hasSectionStructure && !string.IsNullOrWhiteSpace(scheduleItem.BiblePublicationSectionName))
            {
                subtitleParts.Add($"* {scheduleItem.BiblePublicationSectionName}");
            }

            if (!string.IsNullOrWhiteSpace(scheduleItem.BiblePublicationTrackTitle))
            {
                subtitleParts.Add($"* {scheduleItem.BiblePublicationTrackTitle}");
            }
            else if (hasSectionStructure)
            {
                var trackNumber = scheduleItem.BiblePublicationTrackNumber.HasValue && scheduleItem.BiblePublicationTrackNumber.Value > 0
                    ? scheduleItem.BiblePublicationTrackNumber.Value.ToString()
                    : null;
                if (trackNumber != null)
                {
                    subtitleParts.Add($"* {trackNumber}");
                }
            }
        }

        // Add Language (at the end)
        var languageName = !string.IsNullOrWhiteSpace(scheduleItem.BiblePublicationLanguageName)
            ? scheduleItem.BiblePublicationLanguageName
            : scheduleItem.BiblePublicationLanguageCode ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(languageName))
        {
            // Add music note emoji (🎵) to the right of language text if music is enabled
            var languageText = scheduleItem.MusicEnabled
                ? $"{languageName} 🎵"
                : languageName;
            subtitleParts.Add($"* {languageText}");
        }

        if (subtitleParts.Count > 0)
        {
            return string.Join(" ", subtitleParts);
        }

        // Fallback: show status and time
        var fallbackStatusText = scheduleItem.IsEnabled ? "Enabled" : "Disabled";
        var fallbackTimeText = scheduleItem.TimeText;
        return $"* {fallbackStatusText} * {fallbackTimeText}";
    }
}

