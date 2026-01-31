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

    // Unicode directional control characters for RTL support
    private const char RightToLeftMark = '\u200F';
    private const char LeftToRightMark = '\u200E';

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
    /// Format for sectioned Bible: "Section Name - Track Number" (e.g., "Genesis - 1")
    /// Format for drama/video: Track Title (e.g., "Adam and Eve in the Garden of Eden")
    /// </summary>
    public static string BuildScheduleTitle(ScheduleStateItem scheduleItem)
    {
        if (scheduleItem.BiblePublicationScheduleId.HasValue)
        {
            var isRtl = string.Equals(scheduleItem.BiblePublicationLanguageDirection, "rtl", StringComparison.OrdinalIgnoreCase);
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
                    return isRtl ? $"{RightToLeftMark}{title}" : title;
                }
            }
            else
            {
                // Drama/Video: Show track title
                if (!string.IsNullOrWhiteSpace(scheduleItem.BiblePublicationTrackTitle))
                {
                    var title = scheduleItem.BiblePublicationTrackTitle;
                    return isRtl ? $"{RightToLeftMark}{title}" : title;
                }
            }
        }

        // Fallback: return schedule name
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
        var isRtl = string.Equals(scheduleItem.BiblePublicationLanguageDirection, "rtl", StringComparison.OrdinalIgnoreCase);

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
            // Apply RTL marker to the language name if needed
            var rtlLanguageName = isRtl ? $"{RightToLeftMark}{languageName}" : languageName;

            // Add music note emoji if music is enabled
            var languageText = scheduleItem.MusicEnabled
                ? $"{rtlLanguageName} 🎵"
                : rtlLanguageName;
            subtitleParts.Add($"* {languageText}");
        }

        if (subtitleParts.Count > 0)
        {
            var subtitle = string.Join(" ", subtitleParts);
            // Apply RTL marker to the entire subtitle if RTL
            return isRtl ? $"{RightToLeftMark}{subtitle}" : subtitle;
        }

        // Fallback: show status and time
        var fallbackStatusText = scheduleItem.IsEnabled ? "Enabled" : "Disabled";
        var fallbackTimeText = scheduleItem.TimeText;
        return $"* {fallbackStatusText} * {fallbackTimeText}";
    }
}