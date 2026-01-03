#nullable enable
using Bible;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Stores.Models;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Bible.Alarm.ViewModels.HomeViewModelHelpers;

/// <summary>
/// Manages ScheduleListItemViewModel creation, updates, and lifecycle.
/// Separated from HomeViewModel for better modularity.
/// </summary>
public class ScheduleViewModelManager
{
    private readonly ILogger logger;
    private readonly IServiceProvider serviceProvider;
    private readonly Dictionary<int, ScheduleListItemViewModel> scheduleViewModels = [];
    private readonly Action<int> trackPlayClick;

    public ScheduleViewModelManager(
        ILogger logger,
        IServiceProvider serviceProvider,
        Action<int> trackPlayClick)
    {
        this.logger = logger;
        this.serviceProvider = serviceProvider;
        this.trackPlayClick = trackPlayClick;
    }

    public Dictionary<int, ScheduleListItemViewModel> ScheduleViewModels => scheduleViewModels;

    public (List<ScheduleListItemViewModel> schedulesToAdd, List<int> schedulesToRemove, ObservableHashSet<ScheduleListItemViewModel> newSchedules) PrepareScheduleViewModelsOnUIThread(
        Dictionary<int, AlarmSchedule> scheduleDataMap,
        Dictionary<int, ScheduleStateItem> scheduleStateItemMap,
        ObservableHashSet<ScheduleListItemViewModel> currentSchedules)
    {
        var schedulesToAdd = new List<ScheduleListItemViewModel>();
        var schedulesToRemove = new List<int>();
        var currentViewModelIds = new HashSet<int>(scheduleDataMap.Keys);

        // Process each schedule using pre-mapped data
        foreach (var (scheduleId, schedule) in scheduleDataMap)
        {
            scheduleStateItemMap.TryGetValue(scheduleId, out var scheduleStateItem);

            if (scheduleViewModels.TryGetValue(scheduleId, out var existingViewModel))
            {
                // Existing view model - update it with pre-mapped data
                existingViewModel.InitializeFromSchedule(schedule, scheduleStateItem);

                // Ensure callbacks are set
                if (existingViewModel.OnPlayStarted == null)
                {
                    existingViewModel.OnPlayStarted = () => trackPlayClick(scheduleId);
                }
                if (existingViewModel.OnPlaybackStarted == null)
                {
                    existingViewModel.OnPlaybackStarted = () => { };
                }
            }
            else
            {
                // New schedule - create new view model
                logger.Debug("PrepareScheduleViewModels: Creating new ScheduleListItem for schedule {ScheduleId}", scheduleId);

                var viewModel = serviceProvider.GetRequiredService<ScheduleListItemViewModel>();
                viewModel.InitializeFromSchedule(schedule, scheduleStateItem);

                if (viewModel.Schedule == null)
                {
                    logger.Warning("PrepareScheduleViewModels: ScheduleListItem for schedule {ScheduleId} was not initialized properly (Schedule is null)", scheduleId);
                }
                else
                {
                    logger.Debug("PrepareScheduleViewModels: ScheduleListItem for schedule {ScheduleId} initialized successfully with name '{Name}'",
                        scheduleId, viewModel.Schedule.Name);
                }

                // Set callbacks
                viewModel.OnPlayStarted = () => trackPlayClick(scheduleId);
                viewModel.OnPlaybackStarted = () => { };
                scheduleViewModels[scheduleId] = viewModel;
                schedulesToAdd.Add(viewModel);
            }
        }

        // Identify view models to remove
        var toRemove = scheduleViewModels.Keys.Where(id => !currentViewModelIds.Contains(id)).ToList();
        foreach (var id in toRemove)
        {
            if (scheduleViewModels.TryGetValue(id, out var viewModel))
            {
                schedulesToRemove.Add(id);
                viewModel.Dispose();
                scheduleViewModels.Remove(id);
            }
        }

        // Prepare new collection
        var newSchedules = new ObservableHashSet<ScheduleListItemViewModel>();
        var currentSchedulesSnapshot = currentSchedules.ToList();

        // Add all existing items that aren't being removed
        foreach (var item in currentSchedulesSnapshot)
        {
            if (item.ScheduleId > 0 && !schedulesToRemove.Contains(item.ScheduleId))
            {
                newSchedules.Add(item);
            }
        }

        // Add new items
        foreach (var item in schedulesToAdd)
        {
            newSchedules.Add(item);
        }

        return (schedulesToAdd, schedulesToRemove, newSchedules);
    }

    public void UpdateScheduleViewModels(ObservableHashSet<ScheduleStateItem> scheduleItems)
    {
        var scheduleItemsSnapshot = scheduleItems.ToList();
        logger.Debug("ScheduleViewModelManager: UpdateScheduleViewModels called with {Count} schedule items from state.", scheduleItemsSnapshot.Count);

        foreach (var scheduleItem in scheduleItemsSnapshot)
        {
            var scheduleId = scheduleItem.Id;
            if (scheduleId <= 0)
            {
                logger.Warning("ScheduleViewModelManager: Skipping update for invalid schedule ID {ScheduleId}", scheduleId);
                continue;
            }

            if (scheduleViewModels.TryGetValue(scheduleId, out var existingViewModel))
            {
                logger.Debug("ScheduleViewModelManager: Updating existing ViewModel for schedule {ScheduleId}. DaysOfWeek: {DaysOfWeek}", 
                    scheduleId, scheduleItem.DaysOfWeek);
                // Update the view model with latest state
                // Note: SetScheduleId may fail if schedule is not in state yet (timing issue),
                // but OnApplicationStateChanged will handle the update when state is ready
                try
                {
                    existingViewModel.SetScheduleId(scheduleId);
                    logger.Debug("ScheduleViewModelManager: Successfully updated ViewModel for schedule {ScheduleId}", scheduleId);
                }
                catch (Exception ex)
                {
                    // Schedule not found in state yet - OnApplicationStateChanged will handle it
                    // This can happen due to async state updates
                    logger.Warning(ex, "ScheduleViewModelManager: Failed to update ViewModel for schedule {ScheduleId}. OnApplicationStateChanged will handle it when state is ready.", scheduleId);
                }
            }
            else
            {
                logger.Warning("ScheduleViewModelManager: ViewModel for schedule {ScheduleId} not found in dictionary, cannot update.", scheduleId);
            }
        }
    }

    public void DisposeAll()
    {
        foreach (var viewModel in scheduleViewModels.Values)
        {
            viewModel.Dispose();
        }
        scheduleViewModels.Clear();
    }
}

