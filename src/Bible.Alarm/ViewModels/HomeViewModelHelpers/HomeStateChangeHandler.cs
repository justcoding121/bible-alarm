#nullable enable
using Bible;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Microsoft.Maui.Essentials;
using Serilog;

namespace Bible.Alarm.ViewModels.HomeViewModelHelpers;

/// <summary>
/// Handles state change processing for HomeViewModel.
/// Separated from HomeViewModel for better modularity.
/// </summary>
public class HomeStateChangeHandler
{
    private readonly ILogger logger;
    private readonly ScheduleDataPreparer dataPreparer;
    private readonly ScheduleViewModelManager viewModelManager;
    private readonly ProgressBarAnimator progressAnimator;
    private readonly Action<bool> setIsBusy;
    private readonly Action<double> setProgressBarOpacity;
    private readonly Func<bool> getIsBusy;
    private readonly Func<ObservableHashSet<ScheduleListItemViewModel>?> getSchedules;
    private readonly Action<ObservableHashSet<ScheduleListItemViewModel>> setSchedules;
    private readonly Action updateProgressBarVisibility;
    private readonly Func<Task> fadeOutProgressBarAsync;

    private int? lastProcessedSchedulesCount;
    private HashSet<int>? lastProcessedScheduleIds;
    private Dictionary<int, (int? BookNumber, int? ChapterNumber, string Name, int Hour, int Minute, DaysOfWeek DaysOfWeek)>? lastProcessedScheduleProperties;

    public HomeStateChangeHandler(
        ILogger logger,
        ScheduleDataPreparer dataPreparer,
        ScheduleViewModelManager viewModelManager,
        ProgressBarAnimator progressAnimator,
        Action<bool> setIsBusy,
        Action<double> setProgressBarOpacity,
        Func<bool> getIsBusy,
        Func<ObservableHashSet<ScheduleListItemViewModel>?> getSchedules,
        Action<ObservableHashSet<ScheduleListItemViewModel>> setSchedules,
        Action updateProgressBarVisibility,
        Func<Task> fadeOutProgressBarAsync)
    {
        this.logger = logger;
        this.dataPreparer = dataPreparer;
        this.viewModelManager = viewModelManager;
        this.progressAnimator = progressAnimator;
        this.setIsBusy = setIsBusy;
        this.setProgressBarOpacity = setProgressBarOpacity;
        this.getIsBusy = getIsBusy;
        this.getSchedules = getSchedules;
        this.setSchedules = setSchedules;
        this.updateProgressBarVisibility = updateProgressBarVisibility;
        this.fadeOutProgressBarAsync = fadeOutProgressBarAsync;
    }

    public async Task HandleStateChangedAsync(ApplicationState stateValue)
    {
        if (stateValue.Schedules != null)
        {
            // Check if Schedules collection has actually changed
            var currentScheduleIds = new HashSet<int>(stateValue.Schedules.Where(s => s.Id > 0).Select(s => s.Id));
            var schedulesCountChanged = lastProcessedSchedulesCount != stateValue.Schedules.Count;
            var scheduleIdsChanged = lastProcessedScheduleIds == null || !lastProcessedScheduleIds.SetEquals(currentScheduleIds);

            // Check if schedule properties (like chapter number, name, time, days of week) have changed
            var currentScheduleProperties = stateValue.Schedules
                .Where(s => s.Id > 0)
                .ToDictionary(s => s.Id, s => (
                    BookNumber: s.BibleReadingBookNumber, 
                    ChapterNumber: s.BibleReadingChapterNumber,
                    Name: s.Name ?? string.Empty,
                    Hour: s.Hour,
                    Minute: s.Minute,
                    DaysOfWeek: s.DaysOfWeek));
            var schedulePropertiesChanged = lastProcessedScheduleProperties == null ||
                currentScheduleProperties.Any(kvp => 
                    !lastProcessedScheduleProperties.ContainsKey(kvp.Key) ||
                    lastProcessedScheduleProperties[kvp.Key].BookNumber != kvp.Value.BookNumber ||
                    lastProcessedScheduleProperties[kvp.Key].ChapterNumber != kvp.Value.ChapterNumber ||
                    lastProcessedScheduleProperties[kvp.Key].Name != kvp.Value.Name ||
                    lastProcessedScheduleProperties[kvp.Key].Hour != kvp.Value.Hour ||
                    lastProcessedScheduleProperties[kvp.Key].Minute != kvp.Value.Minute ||
                    lastProcessedScheduleProperties[kvp.Key].DaysOfWeek != kvp.Value.DaysOfWeek);

            if (!schedulesCountChanged && !scheduleIdsChanged && !schedulePropertiesChanged && lastProcessedScheduleIds != null)
            {
                logger.Debug("OnStateChanged: Skipping processing - Schedules collection unchanged. Count: {Count}", stateValue.Schedules.Count);
                
                // Even if skipping processing, ensure progress bar is hidden if we have schedules
                if (stateValue.Schedules.Count > 0 && getIsBusy())
                {
                    setIsBusy(false);
                    await fadeOutProgressBarAsync();
                }
                return;
            }

            logger.Debug("OnStateChanged: Processing {Count} schedules from state. Current Schedules count: {CurrentCount}",
                stateValue.Schedules.Count, getSchedules()?.Count ?? 0);

            var hadSchedules = getSchedules() != null && getSchedules()!.Count > 0;
            var previousScheduleCount = getSchedules()?.Count ?? 0;
            var currentScheduleCount = stateValue.Schedules.Count;

            // If we had schedules but now don't (cleared), reset progress bar flag
            if (hadSchedules && (stateValue.Schedules == null || stateValue.Schedules.Count == 0))
            {
                setIsBusy(true);
                updateProgressBarVisibility();
            }

            // Prepare data structures off UI thread
            var (scheduleDataMap, scheduleStateItemMap) = await Task.Run(() =>
            {
                return dataPreparer.PrepareScheduleDataOffUIThread(stateValue.Schedules ?? new ObservableHashSet<ScheduleStateItem>());
            });

            // Prepare ViewModels and collection on UI thread
            var currentSchedules = getSchedules() ?? new ObservableHashSet<ScheduleListItemViewModel>();
            var (schedulesToAdd, schedulesToRemove, newSchedules) = viewModelManager.PrepareScheduleViewModelsOnUIThread(
                scheduleDataMap,
                scheduleStateItemMap,
                currentSchedules);
            var hasSchedulesNow = newSchedules != null && newSchedules.Count > 0;

            // Show progress bar when delete is detected
            if (schedulesToRemove.Count > 0 && previousScheduleCount > currentScheduleCount)
            {
                logger.Debug("OnStateChanged: Delete detected - showing progress bar. Removing {Count} schedules", schedulesToRemove.Count);
                setIsBusy(true);
                updateProgressBarVisibility();
            }

            logger.Debug("OnStateChanged: Prepared {AddCount} to add, {RemoveCount} to remove, {NewCount} total. HasSchedulesNow: {HasSchedules}",
                schedulesToAdd.Count, schedulesToRemove.Count, newSchedules?.Count ?? 0, hasSchedulesNow);

            // Yield once to allow UI to process any pending updates
            await Task.Yield();

            var isInitialLoad = getSchedules() == null || getSchedules()!.Count == 0;

            if (isInitialLoad && hasSchedulesNow && newSchedules != null)
            {
                logger.Debug("OnStateChanged: Initial load - setting {Count} schedules via property setter", newSchedules.Count);
                
                var schedulesCollection = BuildScheduleCollection(newSchedules, schedulesToRemove);
                setSchedules(schedulesCollection);
                logger.Debug("OnStateChanged: Initial load complete. Collection now has {Count} items", getSchedules()?.Count ?? 0);
            }
            else if (schedulesToAdd.Count > 0 || schedulesToRemove.Count > 0)
            {
                logger.Debug("OnStateChanged: Updating collection - Adding {AddCount}, Removing {RemoveCount}",
                    schedulesToAdd.Count, schedulesToRemove.Count);

                if (newSchedules != null)
                {
                    var updatedCollection = BuildScheduleCollection(newSchedules, schedulesToRemove);
                    setSchedules(updatedCollection);
                    logger.Debug("OnStateChanged: Collection updated. Now has {Count} items", getSchedules()?.Count ?? 0);
                }

                if (schedulesToRemove.Count > 0)
                {
                    setIsBusy(false);
                    await fadeOutProgressBarAsync();
                }
            }
            else
            {
                // No collection change, but still update existing items
                if (stateValue.Schedules != null)
                {
                    viewModelManager.UpdateScheduleViewModels(stateValue.Schedules);
                }

                if (schedulesToAdd.Count > 0 && previousScheduleCount < currentScheduleCount)
                {
                    logger.Debug("OnStateChanged: Delete rollback detected - hiding progress bar");
                    setIsBusy(false);
                    await fadeOutProgressBarAsync();
                }
            }

            // Only set IsBusy to false if we actually have schedules now
            if (hasSchedulesNow && getIsBusy())
            {
                setIsBusy(false);
                await fadeOutProgressBarAsync();
            }

            // Update last processed state
            if (stateValue.Schedules != null)
            {
                lastProcessedSchedulesCount = stateValue.Schedules.Count;
                lastProcessedScheduleIds = currentScheduleIds;
                lastProcessedScheduleProperties = currentScheduleProperties;
            }
        }
        else
        {
            logger.Debug("OnStateChanged: State.Schedules is null, showing loading state");
            updateProgressBarVisibility();
            lastProcessedSchedulesCount = null;
            lastProcessedScheduleIds = null;
            lastProcessedScheduleProperties = null;
        }
    }

    /// <summary>
    /// Builds a new schedule collection by keeping existing items (excluding those to remove) and adding new items.
    /// </summary>
    private ObservableHashSet<ScheduleListItemViewModel> BuildScheduleCollection(
        ObservableHashSet<ScheduleListItemViewModel> newSchedules,
        List<int> schedulesToRemove)
    {
        var collection = new ObservableHashSet<ScheduleListItemViewModel>();
        
        // Add all existing items that aren't being removed
        foreach (var existingItem in getSchedules() ?? [])
        {
            if (!schedulesToRemove.Contains(existingItem.ScheduleId))
            {
                collection.Add(existingItem);
            }
        }

        // Add new items
        foreach (var item in newSchedules)
        {
            logger.Debug("OnStateChanged: Adding schedule {ScheduleId} ({Name}) to collection",
                item.ScheduleId, item.Name);
            collection.Add(item);
        }

        return collection;
    }
}

