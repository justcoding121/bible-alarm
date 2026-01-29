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

