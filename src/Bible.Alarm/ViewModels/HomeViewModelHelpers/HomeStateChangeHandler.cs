#nullable enable
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Microsoft.Maui.ApplicationModel;
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
    private readonly Action? notifySchedulesChanged;
    private readonly Action updateProgressBarVisibility;
    private readonly Func<Task> fadeOutProgressBarAsync;
    private readonly Func<bool> isPlaybackModalVisible;

    private int? lastProcessedSchedulesCount;
    private HashSet<int>? lastProcessedScheduleIds;
    private Dictionary<int, (string? SectionCode, string? TrackCode, string Name, int Hour, int Minute, DaysOfWeek DaysOfWeek, DateTime? LastPlayedAtUtc)>? lastProcessedScheduleProperties;

    // Stored when a reorder is deferred because the playback modal is visible.
    // Applied by ApplyDeferredReorderAsync() when the modal is confirmed on screen.
    private ObservableHashSet<ScheduleListItemViewModel>? deferredNewSchedules;
    private Dictionary<int, (string? SectionCode, string? TrackCode, string Name, int Hour, int Minute, DaysOfWeek DaysOfWeek, DateTime? LastPlayedAtUtc)>? deferredScheduleProperties;

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
        Action? notifySchedulesChanged,
        Action updateProgressBarVisibility,
        Func<Task> fadeOutProgressBarAsync,
        Func<bool> isPlaybackModalVisible)
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
        this.notifySchedulesChanged = notifySchedulesChanged;
        this.updateProgressBarVisibility = updateProgressBarVisibility;
        this.fadeOutProgressBarAsync = fadeOutProgressBarAsync;
        this.isPlaybackModalVisible = isPlaybackModalVisible;
    }

    public async Task HandleStateChangedAsync(ApplicationState stateValue)
    {
        var deferReorder = false;
        if (stateValue.Schedules != null)
        {
            // Check if Schedules collection has actually changed
            var currentScheduleIds = new HashSet<int>(stateValue.Schedules.Where(s => s.Id > 0).Select(s => s.Id));
            var schedulesCountChanged = lastProcessedSchedulesCount != stateValue.Schedules.Count;
            var scheduleIdsChanged = lastProcessedScheduleIds == null || !lastProcessedScheduleIds.SetEquals(currentScheduleIds);

            // Check if schedule properties (like track number, name, time, days of week, last played) have changed
            var currentScheduleProperties = stateValue.Schedules
                .Where(s => s.Id > 0)
                .ToDictionary(s => s.Id, s => (
                    SectionCode: s.BiblePublicationSectionCode,
                    TrackCode: s.BiblePublicationTrackCode,
                    Name: s.Name ?? string.Empty,
                    Hour: s.Hour,
                    Minute: s.Minute,
                    DaysOfWeek: s.DaysOfWeek,
                    LastPlayedAtUtc: s.LastPlayedAtUtc));
            var schedulePropertiesChanged = lastProcessedScheduleProperties == null ||
                currentScheduleProperties.Any(kvp =>
                    !lastProcessedScheduleProperties.ContainsKey(kvp.Key) ||
                    !string.Equals(lastProcessedScheduleProperties[kvp.Key].SectionCode, kvp.Value.SectionCode, StringComparison.OrdinalIgnoreCase) ||
                    lastProcessedScheduleProperties[kvp.Key].TrackCode != kvp.Value.TrackCode ||
                    lastProcessedScheduleProperties[kvp.Key].Name != kvp.Value.Name ||
                    lastProcessedScheduleProperties[kvp.Key].Hour != kvp.Value.Hour ||
                    lastProcessedScheduleProperties[kvp.Key].Minute != kvp.Value.Minute ||
                    lastProcessedScheduleProperties[kvp.Key].DaysOfWeek != kvp.Value.DaysOfWeek ||
                    Nullable.Compare(lastProcessedScheduleProperties[kvp.Key].LastPlayedAtUtc, kvp.Value.LastPlayedAtUtc) != 0);

            if (!schedulesCountChanged && !scheduleIdsChanged && !schedulePropertiesChanged && lastProcessedScheduleIds != null)
            {
                logger.Debug(AppConstants.Logging.HomeStateChangeHandlerDiagnosticsLog.SkippingProcessingSchedulesUnchangedCount, stateValue.Schedules.Count);

                // Even if skipping processing, ensure progress bar is hidden if we have schedules
                if (stateValue.Schedules.Count > 0 && getIsBusy())
                {
                    // Fade out first, then set IsBusy to false
                    // This ensures smooth animation before UpdateVisibility snaps opacity to 0
                    await fadeOutProgressBarAsync();
                    setIsBusy(false);
                }
                return;
            }

            logger.Debug(AppConstants.Logging.HomeStateChangeHandlerDiagnosticsLog.ProcessingSchedulesFromStateCurrentCollectionCount,
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

            // Capture the pre-mutation sort order so we can detect whether items actually moved.
            // PrepareScheduleViewModelsOnUIThread mutates existing VM instances (via InitializeFromSchedule),
            // which changes LastPlayedAtUtc and invalidates the SortedSet's internal ordering.
            // We must snapshot the order NOW before those mutations happen.
            var oldScheduleOrder = currentSchedules.Select(vm => vm.ScheduleId).ToList();

            var (schedulesToAdd, schedulesToRemove, newSchedules) = viewModelManager.PrepareScheduleViewModelsOnUIThread(
                scheduleDataMap,
                scheduleStateItemMap,
                currentSchedules);

            // newSchedules is a fresh ObservableHashSet built from the mutated VMs, so its iteration
            // order reflects the true post-mutation sort order.
            var newScheduleOrder = newSchedules?.Select(vm => vm.ScheduleId).ToList() ?? [];
            var orderChanged = !oldScheduleOrder.SequenceEqual(newScheduleOrder);
            var hasSchedulesNow = newSchedules != null && newSchedules.Count > 0;

            // Show progress bar when delete is detected
            if (schedulesToRemove.Count > 0 && previousScheduleCount > currentScheduleCount)
            {
                logger.Debug(AppConstants.Logging.HomeStateChangeHandlerDiagnosticsLog.DeleteDetectedShowingProgressRemovingCount, schedulesToRemove.Count);
                setIsBusy(true);
                updateProgressBarVisibility();
            }

            logger.Debug(AppConstants.Logging.HomeStateChangeHandlerDiagnosticsLog.PreparedAddRemoveTotalsHasSchedulesNow,
                schedulesToAdd.Count, schedulesToRemove.Count, newSchedules?.Count ?? 0, hasSchedulesNow);

            var isInitialLoad = getSchedules() == null || getSchedules()!.Count == 0;
            var fadeDeferredForInitialLoad = false;

            if (isInitialLoad && hasSchedulesNow && newSchedules != null)
            {
                logger.Debug(AppConstants.Logging.HomeStateChangeHandlerDiagnosticsLog.InitialLoadSettingSchedulesCount, newSchedules.Count);

                SyncCollectionToNewSchedules(newSchedules);
                notifySchedulesChanged?.Invoke();
                logger.Debug(AppConstants.Logging.HomeStateChangeHandlerDiagnosticsLog.InitialLoadCompleteDeferringProgressBarHideUntilItemsRender, newSchedules.Count);

                fadeDeferredForInitialLoad = true;
                _ = DeferProgressBarHideUntilListRenderedAsync();
            }
            else if (schedulesToAdd.Count > 0 || schedulesToRemove.Count > 0)
            {
                logger.Debug(AppConstants.Logging.HomeStateChangeHandlerDiagnosticsLog.UpdatingCollectionAddingRemoving,
                    schedulesToAdd.Count, schedulesToRemove.Count);

                if (newSchedules != null)
                {
                    SyncCollectionToNewSchedules(newSchedules);
                    notifySchedulesChanged?.Invoke();
                    logger.Debug(AppConstants.Logging.HomeStateChangeHandlerDiagnosticsLog.CollectionUpdatedNowHasCount, getSchedules()?.Count ?? 0);
                }

                if (schedulesToRemove.Count > 0)
                {
                    await fadeOutProgressBarAsync();
                    setIsBusy(false);
                }
            }
            else
            {
                // No add/remove, but schedule properties (e.g. LastPlayedAtUtc) may have changed — sync to reorder
                deferReorder = schedulePropertiesChanged && orderChanged && newSchedules != null && newSchedules.Count > 0 && isPlaybackModalVisible();
                if (schedulePropertiesChanged && newSchedules != null && newSchedules.Count > 0 && !deferReorder)
                {
                    if (orderChanged)
                    {
                        // Items moved positions (e.g. a different schedule became most-recently-played).
                        // Replace the collection so the CollectionView reflects the new sort order.
                        logger.Debug(AppConstants.Logging.HomeStateChangeHandlerDiagnosticsLog.PropertiesChangedSortOrderChangedSyncingCollectionToReorder);
                        SyncCollectionToNewSchedules(newSchedules);
                        notifySchedulesChanged?.Invoke();
                    }
                    else
                    {
                        // Only metadata changed (e.g. track/section code during playback transition).
                        // The ScheduleListItemViewModel instances were already updated in-place by
                        // PrepareScheduleViewModelsOnUIThread via InitializeFromSchedule, which fires
                        // INotifyPropertyChanged. The CollectionView cells update through their bindings
                        // without a collection change event, so no ItemsSource replacement is needed.
                        // Replacing ItemsSource on WinUI causes a full re-render of the entire list,
                        // which is the bug this branch avoids.
                        logger.Debug(AppConstants.Logging.HomeStateChangeHandlerDiagnosticsLog.PropertiesChangedSortOrderUnchangedSkippingCollectionSync);
                    }
                }
                else if (deferReorder)
                {
                    logger.Debug(AppConstants.Logging.HomeStateChangeHandlerDiagnosticsLog.PropertiesChangedPlaybackModalVisibleDeferringListReorder);
                    deferredNewSchedules = newSchedules;
                    deferredScheduleProperties = currentScheduleProperties;
                }
                else if (stateValue.Schedules != null)
                {
                    viewModelManager.UpdateScheduleViewModels(stateValue.Schedules);
                }

                if (schedulesToAdd.Count > 0 && previousScheduleCount < currentScheduleCount)
                {
                    logger.Debug(AppConstants.Logging.HomeStateChangeHandlerDiagnosticsLog.DeleteRollbackDetectedHidingProgressBar);
                    // Fade out first, then set IsBusy to false
                    await fadeOutProgressBarAsync();
                    setIsBusy(false);
                }
            }

            // Hide progress bar for non-initial-load cases (e.g., schedule updates)
            // Initial load defers the hide until list items are rendered
            if (hasSchedulesNow && getIsBusy() && !fadeDeferredForInitialLoad)
            {
                await fadeOutProgressBarAsync();
                setIsBusy(false);
            }

            // Update last processed state
            if (stateValue.Schedules != null)
            {
                lastProcessedSchedulesCount = stateValue.Schedules.Count;
                lastProcessedScheduleIds = currentScheduleIds;
                if (!deferReorder)
                {
                    lastProcessedScheduleProperties = currentScheduleProperties;
                }
            }
        }
        else
        {
            logger.Debug(AppConstants.Logging.HomeStateChangeHandlerDiagnosticsLog.StateSchedulesNullShowingLoading);
            updateProgressBarVisibility();
            lastProcessedSchedulesCount = null;
            lastProcessedScheduleIds = null;
            lastProcessedScheduleProperties = null;
        }
    }

    private const int ListRenderDelayMs = 300;

    private async Task DeferProgressBarHideUntilListRenderedAsync()
    {
        try
        {
            await Task.Delay(ListRenderDelayMs);
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                if (getIsBusy())
                {
                    await fadeOutProgressBarAsync();
                    setIsBusy(false);
                    logger.Debug("OnStateChanged: Progress bar hidden after list render delay");
                }
            });
        }
        catch (OperationCanceledException)
        {
            // Expected if disposed/cancelled
        }
    }

    /// <summary>
    /// Applies a reorder that was deferred because the playback modal was covering the home page.
    /// Call this when the playback modal is confirmed visible so the reorder happens while the list
    /// is hidden behind the modal — invisible to the user.
    /// If no reorder was deferred this is a no-op.
    /// Fallback: if this is never called (e.g. toast shown instead of modal), the next
    /// HandleStateChangedAsync invocation will detect the stale lastProcessedScheduleProperties
    /// and reorder when the modal is no longer visible.
    /// </summary>
    public Task ApplyDeferredReorderAsync()
    {
        if (deferredNewSchedules == null)
        {
            return Task.CompletedTask;
        }

        var schedulesToApply = deferredNewSchedules;
        var propertiesToApply = deferredScheduleProperties;
        deferredNewSchedules = null;
        deferredScheduleProperties = null;

        return MainThread.InvokeOnMainThreadAsync(() =>
        {
            logger.Debug(AppConstants.Logging.HomeStateChangeHandlerDiagnosticsLog.ApplyDeferredReorderApplyingCount, schedulesToApply.Count);
            SyncCollectionToNewSchedules(schedulesToApply);
            notifySchedulesChanged?.Invoke();

            if (propertiesToApply != null)
            {
                lastProcessedScheduleProperties = propertiesToApply;
            }
        });
    }

    /// <summary>
    /// Syncs the bound collection to match newSchedules.
    /// On WinUI we replace the collection so CollectionView refreshes (in-place Clear+Add often does not update the list).
    /// On iOS we mutate in place so CollectionView reliably refreshes after a long bootstrap (replacing can fail there).
    /// </summary>
    private void SyncCollectionToNewSchedules(ObservableHashSet<ScheduleListItemViewModel> newSchedules)
    {
        var useReplaceStrategy = DeviceInfo.Platform == DevicePlatform.WinUI;

        if (useReplaceStrategy || getSchedules() == null)
        {
            var newCollection = new ObservableHashSet<ScheduleListItemViewModel>();
            foreach (var item in newSchedules)
            {
                logger.Debug(AppConstants.Logging.HomeStateChangeHandlerDiagnosticsLog.AddingScheduleToCollectionScheduleIdAndName,
                    item.ScheduleId, item.Name);
                newCollection.Add(item);
            }
            setSchedules(newCollection);
            return;
        }

        var collection = getSchedules()!;
        collection.Clear();
        foreach (var item in newSchedules)
        {
            logger.Debug(AppConstants.Logging.HomeStateChangeHandlerDiagnosticsLog.AddingScheduleToCollectionScheduleIdAndName,
                item.ScheduleId, item.Name);
            collection.Add(item);
        }
    }
}

