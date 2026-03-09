#nullable enable
using Bible.Alarm.Common;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores.Models;
using Serilog;

namespace Bible.Alarm.Services.Schedule;

/// <summary>
/// Shared helper for building schedule display metadata (title and subtitle) in the same format
/// as the Car Play and Android Auto schedule listing. Used for default metadata on idle screens.
/// </summary>
public static class ScheduleDisplayMetadataHelper
{
    private static readonly ILogger logger = Log.ForContext(typeof(ScheduleDisplayMetadataHelper));

    /// <summary>
    /// Builds the display title for a schedule state item.
    /// Bible category: section name + track (e.g. "Genesis 1"). Other categories: track name.
    /// </summary>
    /// <param name="scheduleItem">The schedule state item.</param>
    /// <param name="musicSymbol">Symbol to append when music is enabled (default "🎵"). Use "♫" (U+266B) for Android Auto and CarPlay.</param>
    public static string BuildScheduleTitle(ScheduleStateItem scheduleItem, string? musicSymbol = null)
    {
        var sym = musicSymbol ?? "🎵";

        if (!scheduleItem.BiblePublicationScheduleId.HasValue)
        {
            var scheduleName = !string.IsNullOrWhiteSpace(scheduleItem.Name)
                ? scheduleItem.Name
                : string.Empty;

            if (string.IsNullOrWhiteSpace(scheduleName))
            {
                return scheduleItem.MusicEnabled ? sym : "Unnamed schedule";
            }

            return scheduleItem.MusicEnabled ? scheduleName + " " + sym : scheduleName;
        }

        var categoryName = scheduleItem.BiblePublicationCategoryName
            ?? JwSourceHelper.GetCategoryName(scheduleItem.BiblePublicationCode ?? string.Empty);
        var isBibleCategory = string.Equals(categoryName, "Bible", StringComparison.OrdinalIgnoreCase);

        string? title = null;
        if (isBibleCategory && !string.IsNullOrWhiteSpace(scheduleItem.BiblePublicationSectionName))
        {
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
            return scheduleItem.MusicEnabled ? title + " " + sym : title;
        }

        var fallbackScheduleName = !string.IsNullOrWhiteSpace(scheduleItem.Name)
            ? scheduleItem.Name
            : string.Empty;

        if (string.IsNullOrWhiteSpace(fallbackScheduleName))
        {
            return scheduleItem.MusicEnabled ? sym : "Unnamed schedule";
        }

        return scheduleItem.MusicEnabled ? fallbackScheduleName + " " + sym : fallbackScheduleName;
    }

    /// <summary>
    /// Builds the display subtitle for a ScheduleStateItem.
    /// Format: Schedule Name • Category • Language • Publication • Section (for non-Bible).
    /// Music icon is shown in the title, not the subtitle (Android Auto / CarPlay / default metadata).
    /// </summary>
    /// <param name="scheduleItem">The schedule state item.</param>
    /// <param name="musicSymbol">Obsolete; kept for API compatibility. Music is displayed in the title.</param>
    public static string BuildScheduleSubtitle(ScheduleStateItem scheduleItem, string? musicSymbol = null)
    {
        _ = musicSymbol;

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
