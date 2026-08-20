#nullable enable
using Bible.Alarm.Services.Schedule.Interfaces;
using Bible.Alarm.ViewModels.Schedule;

namespace Bible.Alarm.ViewModels.ScheduleViewModelHelpers;

/// <summary>
/// Handles container view model management for ScheduleViewModel.
/// </summary>
public sealed class ScheduleContainerManager
{
    private readonly IScheduleContainerService scheduleContainerService;
    private readonly IServiceProvider serviceProvider;

    public ScheduleContainerManager(
        IScheduleContainerService scheduleContainerService,
        IServiceProvider serviceProvider)
    {
        this.scheduleContainerService = scheduleContainerService;
        this.serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Initializes container view models asynchronously.
    /// </summary>
    public async Task InitializeContainerViewModelsAsync(
        Action<BiblePublicationSelectionContainerViewModel?, MusicSelectionContainerViewModel?, NumberOfTrackContainerViewModel?, ScheduleDetailsContainerViewModel?, AlarmSettingsContainerViewModel?> setContainers)
    {
        await scheduleContainerService.InitializeContainersAsync(serviceProvider, setContainers);
    }
}
