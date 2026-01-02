#nullable enable
using Bible.Alarm.Services.Schedule.Interfaces;
using Bible.Alarm.ViewModels.Schedule;
using Serilog;

namespace Bible.Alarm.ViewModels.Schedule.ScheduleViewModel;

/// <summary>
/// Handles container view model management for ScheduleViewModel.
/// </summary>
public sealed class ScheduleContainerManager(ILogger logger)
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
        Action<BibleSelectionContainerViewModel?, MusicSelectionContainerViewModel?, ChaptersSelectionContainerViewModel?, ScheduleDetailsContainerViewModel?> setContainers)
    {
        await scheduleContainerService.InitializeContainersAsync(serviceProvider, setContainers);
    }

    /// <summary>
    /// Waits for containers to be initialized.
    /// </summary>
    public async Task WaitForContainersAsync(
        Func<BibleSelectionContainerViewModel?> getBibleContainer,
        Func<MusicSelectionContainerViewModel?> getMusicContainer,
        Func<ChaptersSelectionContainerViewModel?> getChaptersContainer,
        Func<ScheduleDetailsContainerViewModel?> getDetailsContainer)
    {
        var maxWaitTime = TimeSpan.FromMilliseconds(500);
        var startTime = DateTime.UtcNow;

        while ((getBibleContainer() == null ||
                getMusicContainer() == null ||
                getChaptersContainer() == null ||
                getDetailsContainer() == null) &&
               (DateTime.UtcNow - startTime) < maxWaitTime)
        {
            await Task.Delay(50);
        }

        await Task.Delay(50);
    }

    /// <summary>
    /// Checks if all containers are ready.
    /// </summary>
    public bool AreContainersReady(
        Func<BibleSelectionContainerViewModel?> getBibleContainer,
        Func<MusicSelectionContainerViewModel?> getMusicContainer,
        Func<ChaptersSelectionContainerViewModel?> getChaptersContainer,
        Func<ScheduleDetailsContainerViewModel?> getDetailsContainer)
    {
        return getBibleContainer() != null &&
               getMusicContainer() != null &&
               getChaptersContainer() != null &&
               getDetailsContainer() != null;
    }
}
