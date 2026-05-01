#nullable enable
using System.Linq;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Microsoft.Maui.ApplicationModel;
using Serilog;

namespace Bible.Alarm.ViewModels.HomeViewModelHelpers;

/// <summary>
/// Per-schedule fields used to detect home list reorder vs in-place metadata updates.
/// </summary>
internal readonly record struct SchedulePropertySnapshot(
    string? SectionCode,
    string? TrackCode,
    string Name,
    int Hour,
    int Minute,
    WeekDays DaysOfWeek,
    DateTime? LastPlayedAtUtc);

/// <summary>
/// Handles state change processing for HomeViewModel.
/// Separated from HomeViewModel for better modularity.
/// </summary>
public class HomeStateChangeHandler
{
    private readonly ILogger logger;
    private readonly ScheduleDataPreparer dataPreparer;
    private readonly ScheduleViewModelManager viewModelManager;
    private readonly Action<bool> setIsBusy;
    private readonly Func<bool> getIsBusy;
    private readonly Func<ObservableHashSet<ScheduleListItemViewModel>?> getSchedules;
    private readonly Action<ObservableHashSet<ScheduleListItemViewModel>> setSchedules;
    private readonly Action? notifySchedulesChanged;
    private readonly Action updateProgressBarVisibility;
    private readonly Func<Task> fadeOutProgressBarAsync;
    private readonly Func<bool> isPlaybackModalVisible;

    private int? lastProcessedSchedulesCount;
    private HashSet<int>? lastProcessedScheduleIds;
    private Dictionary<int, SchedulePropertySnapshot>? lastProcessedScheduleProperties;

    // Stored when a reorder is deferred because the playback modal is visible.
    // Applied by ApplyDeferredReorderAsync() when the modal is confirmed on screen.
    private ObservableHashSet<ScheduleListItemViewModel>? deferredNewSchedules;
    private Dictionary<int, SchedulePropertySnapshot>? deferredScheduleProperties;

    private readonly record struct ScheduleProcessingInputs(
        bool UnchangedSinceLastProcessed,
        HashSet<int> CurrentScheduleIds,
        Dictionary<int, SchedulePropertySnapshot> CurrentScheduleProperties,
        bool SchedulePropertiesChanged);

    public HomeStateChangeHandler(HomeStateChangeHandlerDeps deps, HomeStateChangeHandlerCallbacks callbacks)
    {
        logger = deps.Logger;
        dataPreparer = deps.DataPreparer;
        viewModelManager = deps.ViewModelManager;
        setIsBusy = callbacks.SetIsBusy;
        getIsBusy = callbacks.GetIsBusy;
        getSchedules = callbacks.GetSchedules;
        setSchedules = callbacks.SetSchedules;
        notifySchedulesChanged = callbacks.NotifySchedulesChanged;
        updateProgressBarVisibility = callbacks.UpdateProgressBarVisibility;
        fadeOutProgressBarAsync = callbacks.FadeOutProgressBarAsync;
        isPlaybackModalVisible = callbacks.IsPlaybackModalVisible;
    }

    public async Task HandleStateChangedAsync(ApplicationState stateValue)
    {
        if (stateValue.Schedules == null)
        {
            ApplySchedulesClearedTelemetry();
            return;
        }

        await ApplySchedulesStateChangeAsync(stateValue.Schedules);
    }

    private void ApplySchedulesClearedTelemetry()
    {
        logger.Debug(AppConstants.Logging.HomeStateChangeHandlerDiagnosticsLog.StateSchedulesNullShowingLoading);
        updateProgressBarVisibility();
        lastProcessedSchedulesCount = null;
        lastProcessedScheduleIds = null;
        lastProcessedScheduleProperties = null;
    }

    private ScheduleProcessingInputs BuildScheduleProcessingInputs(ObservableHashSet<ScheduleStateItem> schedules)
    {
        var currentScheduleIds = new HashSet<int>(schedules.Where(s => s.Id > 0).Select(s => s.Id));
        var schedulesCountChanged = lastProcessedSchedulesCount != schedules.Count;
        var scheduleIdsChanged =
            lastProcessedScheduleIds == null || !lastProcessedScheduleIds.SetEquals(currentScheduleIds);
        var currentScheduleProperties = BuildSchedulePropertiesMap(schedules);
        var schedulePropertiesChanged =
            SchedulePropertiesHaveChanged(lastProcessedScheduleProperties, currentScheduleProperties);

        var unchanged = lastProcessedScheduleIds != null && !schedulesCountChanged && !scheduleIdsChanged &&
                        !schedulePropertiesChanged;

        return new ScheduleProcessingInputs(
            unchanged,
            currentScheduleIds,
            currentScheduleProperties,
            schedulePropertiesChanged);
    }

    private async Task ApplySchedulesStateChangeAsync(ObservableHashSet<ScheduleStateItem> schedules)
    {
        var inputs = BuildScheduleProcessingInputs(schedules);

        if (inputs.UnchangedSinceLastProcessed)
        {
            logger.Debug(AppConstants.Logging.HomeStateChangeHandlerDiagnosticsLog.SkippingProcessingSchedulesUnchangedCount, schedules.Count);

            if (schedules.Count > 0 && getIsBusy())
            {
                await fadeOutProgressBarAsync();
                setIsBusy(false);
            }

            return;
        }

        logger.Debug(AppConstants.Logging.HomeStateChangeHandlerDiagnosticsLog.ProcessingSchedulesFromStateCurrentCollectionCount,
            schedules.Count, getSchedules()?.Count ?? 0);

        var hadSchedules = getSchedules() != null && getSchedules()!.Count > 0;
        var previousScheduleCount = getSchedules()?.Count ?? 0;
        var currentScheduleCount = schedules.Count;

        ApplyProgressIndicatorsForScheduleCountTransition(hadSchedules, schedules.Count);

        var (scheduleDataMap, scheduleStateItemMap) = await Task.Run(() =>
            dataPreparer.PrepareScheduleDataOffUIThread(schedules));

        var currentSchedules = getSchedules() ?? new ObservableHashSet<ScheduleListItemViewModel>();
        var oldScheduleOrder = currentSchedules.Select(vm => vm.ScheduleId).ToList();

        var (schedulesToAdd, schedulesToRemove, newSchedules) = viewModelManager.PrepareScheduleViewModelsOnUIThread(
            scheduleDataMap,
            scheduleStateItemMap,
            currentSchedules);

        var newScheduleOrder = newSchedules?.Select(vm => vm.ScheduleId).ToList() ?? [];
        var orderChanged = !oldScheduleOrder.SequenceEqual(newScheduleOrder);
        var hasSchedulesNow = newSchedules != null && newSchedules.Count > 0;

        ShowProgressIfDeletesReduceCount(schedulesToRemove.Count, previousScheduleCount, currentScheduleCount);

        logger.Debug(AppConstants.Logging.HomeStateChangeHandlerDiagnosticsLog.PreparedAddRemoveTotalsHasSchedulesNow,
            schedulesToAdd.Count, schedulesToRemove.Count, newSchedules?.Count ?? 0, hasSchedulesNow);

        var (fadeDeferredForInitialLoad, deferReorder) = await ApplyScheduleCollectionMutationsAsync(
            new ScheduleCollectionMutationArgs
            {
                Schedules = schedules,
                Inputs = inputs,
                SchedulesToAdd = schedulesToAdd,
                SchedulesToRemove = schedulesToRemove,
                NewSchedules = newSchedules,
                OrderChanged = orderChanged,
                HasSchedulesNow = hasSchedulesNow,
                PreviousScheduleCount = previousScheduleCount,
                CurrentScheduleCount = currentScheduleCount
            });

        await FinalizeScheduleStateProcessingAsync(schedules, inputs, hasSchedulesNow, fadeDeferredForInitialLoad, deferReorder);
    }

    private void ApplyProgressIndicatorsForScheduleCountTransition(bool hadSchedules, int incomingScheduleCount)
    {
        if (hadSchedules && incomingScheduleCount == 0)
        {
            setIsBusy(true);
            updateProgressBarVisibility();
        }
    }

    private void ShowProgressIfDeletesReduceCount(
        int schedulesToRemoveCount,
        int previousScheduleCount,
        int currentScheduleCount)
    {
        if (schedulesToRemoveCount > 0 && previousScheduleCount > currentScheduleCount)
        {
            logger.Debug(AppConstants.Logging.HomeStateChangeHandlerDiagnosticsLog.DeleteDetectedShowingProgressRemovingCount,
                schedulesToRemoveCount);
            setIsBusy(true);
            updateProgressBarVisibility();
        }
    }

    private async Task<(bool FadeDeferredForInitialLoad, bool DeferReorder)> ApplyScheduleCollectionMutationsAsync(
        ScheduleCollectionMutationArgs args)
    {
        var schedules = args.Schedules;
        var inputs = args.Inputs;
        var schedulesToAdd = args.SchedulesToAdd;
        var schedulesToRemove = args.SchedulesToRemove;
        var newSchedules = args.NewSchedules;
        var orderChanged = args.OrderChanged;
        var hasSchedulesNow = args.HasSchedulesNow;
        var previousScheduleCount = args.PreviousScheduleCount;
        var currentScheduleCount = args.CurrentScheduleCount;

        var isInitialLoad = getSchedules() == null || getSchedules()!.Count == 0;

        if (isInitialLoad && hasSchedulesNow && newSchedules != null)
        {
            return (await ApplyInitialScheduleCollectionLoadAsync(newSchedules), false);
        }

        if (schedulesToAdd.Count > 0 || schedulesToRemove.Count > 0)
        {
            await ApplyStructuralScheduleCollectionChangeAsync(schedulesToAdd, schedulesToRemove, newSchedules);
            return (false, false);
        }

        var deferReorder = await ApplyNonStructuralScheduleUpdatesAsync(
            schedules,
            inputs,
            newSchedules,
            orderChanged,
            schedulesToAdd.Count,
            previousScheduleCount,
            currentScheduleCount);

        return (false, deferReorder);
    }

    private sealed class ScheduleCollectionMutationArgs
    {
        public required ObservableHashSet<ScheduleStateItem> Schedules { get; init; }
        public required ScheduleProcessingInputs Inputs { get; init; }
        public required List<ScheduleListItemViewModel> SchedulesToAdd { get; init; }
        public required List<int> SchedulesToRemove { get; init; }
        public ObservableHashSet<ScheduleListItemViewModel>? NewSchedules { get; init; }
        public required bool OrderChanged { get; init; }
        public required bool HasSchedulesNow { get; init; }
        public required int PreviousScheduleCount { get; init; }
        public required int CurrentScheduleCount { get; init; }
    }

    private async Task<bool> ApplyInitialScheduleCollectionLoadAsync(ObservableHashSet<ScheduleListItemViewModel> newSchedules)
    {
        logger.Debug(AppConstants.Logging.HomeStateChangeHandlerDiagnosticsLog.InitialLoadSettingSchedulesCount, newSchedules.Count);

        SyncCollectionToNewSchedules(newSchedules);
        notifySchedulesChanged?.Invoke();
        logger.Debug(AppConstants.Logging.HomeStateChangeHandlerDiagnosticsLog.InitialLoadCompleteDeferringProgressBarHideUntilItemsRender,
            newSchedules.Count);

        _ = DeferProgressBarHideUntilListRenderedAsync();
        return true;
    }

    private async Task ApplyStructuralScheduleCollectionChangeAsync(
        List<ScheduleListItemViewModel> schedulesToAdd,
        List<int> schedulesToRemove,
        ObservableHashSet<ScheduleListItemViewModel>? newSchedules)
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

    private async Task<bool> ApplyNonStructuralScheduleUpdatesAsync(
        ObservableHashSet<ScheduleStateItem> schedules,
        ScheduleProcessingInputs inputs,
        ObservableHashSet<ScheduleListItemViewModel>? newSchedules,
        bool orderChanged,
        int schedulesToAddCount,
        int previousScheduleCount,
        int currentScheduleCount)
    {
        var deferReorder = ShouldDeferReorderForPlaybackModal(inputs, orderChanged, newSchedules);

        if (inputs.SchedulePropertiesChanged &&
            newSchedules != null &&
            newSchedules.Count > 0 &&
            !deferReorder)
        {
            ApplyPropertyChangedCollectionSync(orderChanged, newSchedules);
        }
        else if (deferReorder)
        {
            logger.Debug(
                AppConstants.Logging.HomeStateChangeHandlerDiagnosticsLog.PropertiesChangedPlaybackModalVisibleDeferringListReorder);
            deferredNewSchedules = newSchedules;
            deferredScheduleProperties = inputs.CurrentScheduleProperties;
        }
        else
        {
            viewModelManager.UpdateScheduleViewModels(schedules);
        }

        await MaybeFadeProgressAfterDeleteRollbackAsync(schedulesToAddCount, previousScheduleCount, currentScheduleCount);

        return deferReorder;
    }

    private bool ShouldDeferReorderForPlaybackModal(
        ScheduleProcessingInputs inputs,
        bool orderChanged,
        ObservableHashSet<ScheduleListItemViewModel>? newSchedules) =>
        inputs.SchedulePropertiesChanged &&
        orderChanged &&
        newSchedules != null &&
        newSchedules.Count > 0 &&
        isPlaybackModalVisible();

    private void ApplyPropertyChangedCollectionSync(
        bool orderChanged,
        ObservableHashSet<ScheduleListItemViewModel> newSchedules)
    {
        if (orderChanged)
        {
            logger.Debug(
                AppConstants.Logging.HomeStateChangeHandlerDiagnosticsLog.PropertiesChangedSortOrderChangedSyncingCollectionToReorder);
            SyncCollectionToNewSchedules(newSchedules);
            notifySchedulesChanged?.Invoke();
        }
        else
        {
            logger.Debug(
                AppConstants.Logging.HomeStateChangeHandlerDiagnosticsLog.PropertiesChangedSortOrderUnchangedSkippingCollectionSync);
        }
    }

    private async Task MaybeFadeProgressAfterDeleteRollbackAsync(
        int schedulesToAddCount,
        int previousScheduleCount,
        int currentScheduleCount)
    {
        if (schedulesToAddCount > 0 && previousScheduleCount < currentScheduleCount)
        {
            logger.Debug(AppConstants.Logging.HomeStateChangeHandlerDiagnosticsLog.DeleteRollbackDetectedHidingProgressBar);
            await fadeOutProgressBarAsync();
            setIsBusy(false);
        }
    }

    private async Task FinalizeScheduleStateProcessingAsync(
        ObservableHashSet<ScheduleStateItem> schedules,
        ScheduleProcessingInputs inputs,
        bool hasSchedulesNow,
        bool fadeDeferredForInitialLoad,
        bool deferReorder)
    {
        if (hasSchedulesNow && getIsBusy() && !fadeDeferredForInitialLoad)
        {
            await fadeOutProgressBarAsync();
            setIsBusy(false);
        }

        lastProcessedSchedulesCount = schedules.Count;
        lastProcessedScheduleIds = inputs.CurrentScheduleIds;

        if (!deferReorder)
        {
            lastProcessedScheduleProperties = inputs.CurrentScheduleProperties;
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
                    logger.Debug(AppConstants.Logging.HomeStateChangeHandlerDiagnosticsLog.ProgressBarHiddenAfterListRenderDelay);
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

    private static Dictionary<int, SchedulePropertySnapshot> BuildSchedulePropertiesMap(IEnumerable<ScheduleStateItem> schedules)
    {
        return schedules
            .Where(s => s.Id > 0)
            .ToDictionary(
                s => s.Id,
                s => new SchedulePropertySnapshot(
                    s.BiblePublicationSectionCode,
                    s.BiblePublicationTrackCode,
                    s.Name ?? string.Empty,
                    s.Hour,
                    s.Minute,
                    s.DaysOfWeek,
                    s.LastPlayedAtUtc));
    }

    private static bool SchedulePropertiesHaveChanged(
        Dictionary<int, SchedulePropertySnapshot>? last,
        Dictionary<int, SchedulePropertySnapshot> current)
    {
        if (last == null)
        {
            return true;
        }

        return current.Any(kvp =>
        {
            if (!last.TryGetValue(kvp.Key, out var prev))
            {
                return true;
            }

            var cur = kvp.Value;
            return !string.Equals(prev.SectionCode, cur.SectionCode, StringComparison.OrdinalIgnoreCase)
                || prev.TrackCode != cur.TrackCode
                || prev.Name != cur.Name
                || prev.Hour != cur.Hour
                || prev.Minute != cur.Minute
                || prev.DaysOfWeek != cur.DaysOfWeek
                || Nullable.Compare(prev.LastPlayedAtUtc, cur.LastPlayedAtUtc) != 0;
        });
    }
}

