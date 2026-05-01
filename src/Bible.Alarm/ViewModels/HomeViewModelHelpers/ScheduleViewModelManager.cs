#nullable enable
using System.Linq;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Stores.Models;
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

        foreach (var (scheduleId, schedule) in scheduleDataMap)
        {
            scheduleStateItemMap.TryGetValue(scheduleId, out var scheduleStateItem);

            if (scheduleViewModels.TryGetValue(scheduleId, out var existingViewModel))
            {
                UpdateExistingScheduleViewModel(existingViewModel, scheduleId, schedule, scheduleStateItem);
            }
            else
            {
                TryCreateScheduleViewModel(scheduleId, schedule, scheduleStateItem, schedulesToAdd);
            }
        }

        CollectRemovedScheduleIds(currentViewModelIds, schedulesToRemove);

        var newSchedules = BuildMergedScheduleCollection(currentSchedules, schedulesToRemove, schedulesToAdd);

        return (schedulesToAdd, schedulesToRemove, newSchedules);
    }

    private void UpdateExistingScheduleViewModel(
        ScheduleListItemViewModel existingViewModel,
        int scheduleId,
        AlarmSchedule schedule,
        ScheduleStateItem? scheduleStateItem)
    {
        existingViewModel.InitializeFromSchedule(schedule, scheduleStateItem);

        if (existingViewModel.OnPlayStarted == null)
        {
            existingViewModel.OnPlayStarted = () => trackPlayClick(scheduleId);
        }

        if (existingViewModel.OnPlaybackStarted == null)
        {
            existingViewModel.OnPlaybackStarted = () => { };
        }
    }

    private void TryCreateScheduleViewModel(
        int scheduleId,
        AlarmSchedule schedule,
        ScheduleStateItem? scheduleStateItem,
        List<ScheduleListItemViewModel> schedulesToAdd)
    {
        logger.Debug(AppConstants.Logging.ScheduleViewModelManagerDiagnosticsLog.PrepareScheduleViewModelsCreatingNewListItem, scheduleId);

        var viewModel = serviceProvider.GetRequiredService<ScheduleListItemViewModel>();
        viewModel.InitializeFromSchedule(schedule, scheduleStateItem);

        if (viewModel.Schedule == null)
        {
            logger.Warning(AppConstants.Logging.ScheduleViewModelManagerDiagnosticsLog.PrepareScheduleViewModelsScheduleListItemNotInitialized, scheduleId);
        }
        else
        {
            logger.Debug(AppConstants.Logging.ScheduleViewModelManagerDiagnosticsLog.PrepareScheduleViewModelsScheduleListItemInitializedWithName,
                scheduleId, viewModel.Schedule.Name);
        }

        viewModel.OnPlayStarted = () => trackPlayClick(scheduleId);
        viewModel.OnPlaybackStarted = () => { };
        scheduleViewModels[scheduleId] = viewModel;
        schedulesToAdd.Add(viewModel);
    }

    private void CollectRemovedScheduleIds(HashSet<int> currentViewModelIds, List<int> schedulesToRemove)
    {
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
    }

    private ObservableHashSet<ScheduleListItemViewModel> BuildMergedScheduleCollection(
        ObservableHashSet<ScheduleListItemViewModel> currentSchedules,
        List<int> schedulesToRemove,
        List<ScheduleListItemViewModel> schedulesToAdd)
    {
        var newSchedules = new ObservableHashSet<ScheduleListItemViewModel>();
        var currentSchedulesSnapshot = currentSchedules.ToList();

        foreach (var item in currentSchedulesSnapshot)
        {
            if (item.ScheduleId > 0 && !schedulesToRemove.Contains(item.ScheduleId))
            {
                newSchedules.Add(item);
            }
        }

        foreach (var item in schedulesToAdd)
        {
            newSchedules.Add(item);
        }

        return newSchedules;
    }

    public void UpdateScheduleViewModels(ObservableHashSet<ScheduleStateItem> scheduleItems)
    {
        var scheduleItemsSnapshot = scheduleItems.ToList();
        logger.Debug(AppConstants.Logging.ScheduleViewModelManagerDiagnosticsLog.UpdateScheduleViewModelsCalledWithCount, scheduleItemsSnapshot.Count);

        foreach (var scheduleItem in scheduleItemsSnapshot)
        {
            var scheduleId = scheduleItem.Id;
            if (scheduleId <= 0)
            {
                logger.Warning(AppConstants.Logging.ScheduleViewModelManagerDiagnosticsLog.SkippingUpdateInvalidScheduleId, scheduleId);
                continue;
            }

            if (scheduleViewModels.TryGetValue(scheduleId, out var existingViewModel))
            {
                logger.Debug(AppConstants.Logging.ScheduleViewModelManagerDiagnosticsLog.UpdatingExistingViewModelDaysOfWeek,
                    scheduleId, scheduleItem.DaysOfWeek);
                // Update the view model with latest state
                // Note: SetScheduleId may fail if schedule is not in state yet (timing issue),
                // but OnApplicationStateChanged will handle the update when state is ready
                try
                {
                    existingViewModel.SetScheduleId(scheduleId);
                    logger.Debug(AppConstants.Logging.ScheduleViewModelManagerDiagnosticsLog.SuccessfullyUpdatedViewModel, scheduleId);
                }
                catch (Exception ex)
                {
                    // Schedule not found in state yet - OnApplicationStateChanged will handle it
                    // This can happen due to async state updates
                    logger.Warning(ex, AppConstants.Logging.ScheduleViewModelManagerDiagnosticsLog.FailedToUpdateViewModelStateNotReady, scheduleId);
                }
            }
            else
            {
                logger.Warning(AppConstants.Logging.ScheduleViewModelManagerDiagnosticsLog.ViewModelNotFoundCannotUpdate, scheduleId);
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

