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

    public void DisposeContainers(SchedulePropertyManager propertyManager)
    {
        if (propertyManager.BibleSelectionContainerViewModel is IDisposable bibleDisposable)
        {
            bibleDisposable.Dispose();
        }
        propertyManager.BibleSelectionContainerViewModel = null;

        if (propertyManager.MusicSelectionContainerViewModel is IDisposable musicDisposable)
        {
            musicDisposable.Dispose();
        }
        propertyManager.MusicSelectionContainerViewModel = null;

        if (propertyManager.NumberOfTrackContainerViewModel is IDisposable tracksDisposable)
        {
            tracksDisposable.Dispose();
        }
        propertyManager.NumberOfTrackContainerViewModel = null;

        if (propertyManager.ScheduleDetailsContainerViewModel is IDisposable detailsDisposable)
        {
            detailsDisposable.Dispose();
        }
        propertyManager.ScheduleDetailsContainerViewModel = null;

        if (propertyManager.AlarmSettingsContainerViewModel is IDisposable alarmSettingsDisposable)
        {
            alarmSettingsDisposable.Dispose();
        }
        propertyManager.AlarmSettingsContainerViewModel = null;
    }
}
