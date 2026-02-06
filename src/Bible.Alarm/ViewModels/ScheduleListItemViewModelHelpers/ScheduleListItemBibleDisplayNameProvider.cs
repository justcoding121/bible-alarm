#nullable enable

using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Fluxor;

namespace Bible.Alarm.ViewModels.ScheduleListItemViewModelHelpers;

internal sealed class ScheduleListItemBibleDisplayNameProvider
{
    private readonly IState<ApplicationState> applicationState;

    public ScheduleListItemBibleDisplayNameProvider(IState<ApplicationState> applicationState)
    {
        this.applicationState = applicationState;
    }

    public bool IsBibleCategory(int scheduleId)
    {
        if (scheduleId <= 0)
        {
            return false;
        }

        var scheduleStateItem = applicationState.Value.Schedules?.FirstOrDefault(s => s.Id == scheduleId);
        if (scheduleStateItem == null)
        {
            return false;
        }

        if (string.Equals(scheduleStateItem.BiblePublicationCategoryName, "Bible", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Fallback: category name isn't persisted and might be temporarily missing.
        // Infer from publication code.
        var inferred = JwSourceHelper.GetCategoryName(scheduleStateItem.BiblePublicationCode ?? string.Empty);
        return string.Equals(inferred, "Bible", StringComparison.OrdinalIgnoreCase);
    }

    public string GetBiblePublicationName(int scheduleId)
    {
        if (scheduleId <= 0)
        {
            return string.Empty;
        }

        var scheduleStateItem = applicationState.Value.Schedules?.FirstOrDefault(s => s.Id == scheduleId);
        return DisplayTextHelper.NormalizeSingleLine(scheduleStateItem?.BiblePublicationName);
    }

    public string GetBiblePublicationSectionName(int scheduleId)
    {
        if (scheduleId <= 0)
        {
            return string.Empty;
        }

        var scheduleStateItem = applicationState.Value.Schedules?.FirstOrDefault(s => s.Id == scheduleId);
        if (scheduleStateItem == null)
        {
            return string.Empty;
        }

        // Only return section name if publication is sectioned
        var hasSectionStructure = PublicationTypeHelper.HasSectionStructure(scheduleStateItem.BiblePublicationCode ?? string.Empty);
        if (hasSectionStructure && !string.IsNullOrWhiteSpace(scheduleStateItem.BiblePublicationSectionName))
        {
            return DisplayTextHelper.NormalizeSingleLine(scheduleStateItem.BiblePublicationSectionName);
        }

        return string.Empty;
    }

    public string GetBiblePublicationSectionAndTrackOneLine(int scheduleId)
    {
        if (scheduleId <= 0)
        {
            return string.Empty;
        }

        var scheduleStateItem = applicationState.Value.Schedules?.FirstOrDefault(s => s.Id == scheduleId);
        if (scheduleStateItem == null)
        {
            return string.Empty;
        }

        // Only for Bible category (per UX requirement).
        if (!IsBibleCategory(scheduleId))
        {
            return string.Empty;
        }

        var sectionName = DisplayTextHelper.NormalizeSingleLine(scheduleStateItem.BiblePublicationSectionName);
        if (string.IsNullOrWhiteSpace(sectionName))
        {
            return string.Empty;
        }

        // Prefer track code (string), fall back to parsing any numeric suffix from track title.
        string? trackCode = !string.IsNullOrWhiteSpace(scheduleStateItem.BiblePublicationTrackCode)
            ? scheduleStateItem.BiblePublicationTrackCode
            : null;

        if (trackCode == null && !string.IsNullOrWhiteSpace(scheduleStateItem.BiblePublicationTrackTitle))
        {
            // e.g. "Chapter 9" -> 9
            var digits = new string(scheduleStateItem.BiblePublicationTrackTitle.Where(char.IsDigit).ToArray());
            if (!string.IsNullOrEmpty(digits))
            {
                trackCode = digits;
            }
        }

        return !string.IsNullOrWhiteSpace(trackCode)
            ? DisplayTextHelper.NormalizeSingleLine($"{sectionName} {trackCode}")
            : sectionName;
    }

    public string GetBiblePublicationTrackName(int scheduleId)
    {
        if (scheduleId <= 0)
        {
            return string.Empty;
        }

        var scheduleStateItem = applicationState.Value.Schedules?.FirstOrDefault(s => s.Id == scheduleId);
        if (scheduleStateItem == null)
        {
            return string.Empty;
        }

        // Use track title if available (contains full name like "Chapter 1")
        if (!string.IsNullOrWhiteSpace(scheduleStateItem.BiblePublicationTrackTitle))
        {
            return DisplayTextHelper.NormalizeSingleLine(scheduleStateItem.BiblePublicationTrackTitle);
        }

        // Fallback: If track title is not available, show track code for sectioned publications
        // This can happen if the track title hasn't been populated yet
        if (!string.IsNullOrWhiteSpace(scheduleStateItem.BiblePublicationTrackCode))
        {
            var hasSectionStructure = PublicationTypeHelper.HasSectionStructure(scheduleStateItem.BiblePublicationCode ?? string.Empty);
            if (hasSectionStructure)
            {
                // For Music publications (e.g., "iam"), show "<PublicationName> <TrackCode>" instead of bare "17".
                var categoryName =
                    scheduleStateItem.BiblePublicationCategoryName
                    ?? JwSourceHelper.GetCategoryName(scheduleStateItem.BiblePublicationCode ?? string.Empty)
                    ?? string.Empty;

                if (string.Equals(categoryName, "Music", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(scheduleStateItem.BiblePublicationName))
                {
                    return DisplayTextHelper.NormalizeSingleLine($"{scheduleStateItem.BiblePublicationName} {scheduleStateItem.BiblePublicationTrackCode}");
                }

                return DisplayTextHelper.NormalizeSingleLine(scheduleStateItem.BiblePublicationTrackCode);
            }
        }

        return string.Empty;
    }
}

