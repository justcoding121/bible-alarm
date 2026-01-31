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

        return string.Equals(scheduleStateItem.BiblePublicationCategoryName, "Bible", StringComparison.OrdinalIgnoreCase);
    }

    public string GetBiblePublicationName(int scheduleId)
    {
        if (scheduleId <= 0)
        {
            return string.Empty;
        }

        var scheduleStateItem = applicationState.Value.Schedules?.FirstOrDefault(s => s.Id == scheduleId);
        return scheduleStateItem?.BiblePublicationName ?? string.Empty;
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
            return scheduleStateItem.BiblePublicationSectionName;
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
        if (!string.Equals(scheduleStateItem.BiblePublicationCategoryName, "Bible", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        var sectionName = scheduleStateItem.BiblePublicationSectionName;
        if (string.IsNullOrWhiteSpace(sectionName))
        {
            return string.Empty;
        }

        // Prefer track number (data-driven), fall back to parsing any numeric suffix from track title.
        int? trackNumber = scheduleStateItem.BiblePublicationTrackNumber is > 0
            ? scheduleStateItem.BiblePublicationTrackNumber
            : null;

        if (trackNumber == null && !string.IsNullOrWhiteSpace(scheduleStateItem.BiblePublicationTrackTitle))
        {
            // e.g. "Chapter 9" -> 9
            var digits = new string(scheduleStateItem.BiblePublicationTrackTitle.Where(char.IsDigit).ToArray());
            if (int.TryParse(digits, out var parsed) && parsed > 0)
            {
                trackNumber = parsed;
            }
        }

        return trackNumber.HasValue
            ? $"{sectionName} {trackNumber.Value}"
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
            return scheduleStateItem.BiblePublicationTrackTitle;
        }

        // Fallback: If track title is not available, show track number for sectioned publications
        // This can happen if the track title hasn't been populated yet
        if (scheduleStateItem.BiblePublicationTrackNumber.HasValue &&
            scheduleStateItem.BiblePublicationTrackNumber.Value > 0)
        {
            var hasSectionStructure = PublicationTypeHelper.HasSectionStructure(scheduleStateItem.BiblePublicationCode ?? string.Empty);
            if (hasSectionStructure)
            {
                return scheduleStateItem.BiblePublicationTrackNumber.Value.ToString();
            }
        }

        return string.Empty;
    }
}

