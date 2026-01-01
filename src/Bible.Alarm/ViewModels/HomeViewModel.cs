#nullable enable

using System.Linq;
using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Database.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.HomeViewModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels;

public sealed class HomeViewModel : ObservableObject, IDisposable
{
    private readonly ILogger logger;
    private readonly IServiceScopeFactory scopeFactory;

    private readonly IDatabaseSeedService databaseSeedService;
    private readonly IScheduleMigrationService scheduleMigrationService;
    private readonly IAlarmScheduleService alarmScheduleService;

    private readonly Func<int, ScheduleListItemViewModel> scheduleListItemFactory;
    private readonly IMapper mapper;

    private readonly IDispatcher dispatcher;
    private readonly IState<ApplicationState> state;
    private readonly IState<PlaybackState> playbackState;
    private readonly INavigationService navigationService;

    // Helper classes
    private readonly ScheduleDataPreparer scheduleDataPreparer;
    private readonly ScheduleViewModelManager scheduleViewModelManager;
    private readonly HomeNavigationHelper navigationHelper;

    // Track last processed state to prevent redundant processing
    private int? lastProcessedSchedulesCount;
    private HashSet<int>? lastProcessedScheduleIds;

    public HomeViewModel(
        ILogger logger,
        IServiceScopeFactory scopeFactory,
        Func<int, ScheduleListItemViewModel> scheduleListItemFactory,
        IState<ApplicationState> state,
        IState<PlaybackState> playbackState,
        IDispatcher dispatcher,
        IDatabaseSeedService databaseSeedService,
        IScheduleMigrationService scheduleMigrationService,
        INavigationService navigationService,
        IAlarmScheduleService alarmScheduleService,
        IMapper mapper)
    {
        this.logger = logger;
        this.scopeFactory = scopeFactory;
        this.scheduleListItemFactory = scheduleListItemFactory;
        this.state = state;
        this.playbackState = playbackState;
        this.dispatcher = dispatcher;
        this.databaseSeedService = databaseSeedService;
        this.scheduleMigrationService = scheduleMigrationService;
        this.alarmScheduleService = alarmScheduleService;
        this.navigationService = navigationService;
        this.mapper = mapper;

        // Initialize helper classes
        scheduleDataPreparer = new ScheduleDataPreparer(mapper);
        navigationHelper = new HomeNavigationHelper(logger, dispatcher, navigationService, state, playbackState, mapper);
        scheduleViewModelManager = new ScheduleViewModelManager(logger, scopeFactory, navigationHelper.TrackPlayClick);

        AddScheduleCommand = new AsyncRelayCommand(async () =>
        {
            // Reset schedule state before creating a new schedule
            // This ensures only one schedule is in state at any time
            dispatcher.Dispatch(new ResetScheduleStateAction());
            await navigationService.NavigateToScheduleAsync();
            dispatcher.Dispatch(new ViewScheduleAction(null));
        });

        ViewScheduleCommand = new AsyncRelayCommand<ScheduleListItemViewModel>(async x =>
        {
            if (x == null || x.Schedule == null)
            {
                return;
            }

            if (navigationHelper.ShouldSkipNavigation(x.Schedule.Id, scheduleViewModelManager.ScheduleViewModels))
            {
                return;
            }

            await navigationHelper.ShowOverlayAndNavigateAsync(x, scheduleViewModelManager.ScheduleViewModels);
        });

        state.StateChanged += OnStateChanged;

        // Immediately process current state when ViewModel is created
        // This ensures the ListView is populated with fresh data when a new Home page is created
        OnStateChanged(this, EventArgs.Empty);

        // Ensure progress bar visibility is updated on initial load
        // This ensures the progress bar shows if schedules aren't loaded yet
        UpdateProgressBarVisibility();

        // Don't set IsBusy = false here - it will be set after initialization completes
        // IsBusy starts as true and will be set to false in OnStateChanged after schedules are populated
    }


    private ObservableHashSet<ScheduleListItemViewModel> schedules = [];

    public ObservableHashSet<ScheduleListItemViewModel> Schedules
    {
        get => schedules;
        set
        {
            SetProperty(ref schedules, value);
            // Update progress bar visibility when schedules collection changes
            UpdateProgressBarVisibility();
        }
    }


    private bool isBusy = true;
    private bool shouldShowProgressBar = true;
    private double progressBarOpacity = 1.0;
    private double animatedProgressStart = 0.0;
    private double animatedProgressEnd = 0.3; // 30% range width
    private System.Timers.Timer? progressAnimationTimer;
    private const double RangeWidth = 0.3; // 30% of the bar width

    /// <summary>
    /// Animated progress start position for indeterminate progress bar range (0.0 to 1.0)
    /// </summary>
    public double AnimatedProgressStart
    {
        get => animatedProgressStart;
        private set => SetProperty(ref animatedProgressStart, value);
    }

    /// <summary>
    /// Animated progress end position for indeterminate progress bar range (0.0 to 1.0)
    /// </summary>
    public double AnimatedProgressEnd
    {
        get => animatedProgressEnd;
        private set => SetProperty(ref animatedProgressEnd, value);
    }

    /// <summary>
    /// Animated progress value (end position) for binding to ProgressBar.Progress
    /// This represents the end of the moving range.
    /// </summary>
    public double AnimatedProgress => AnimatedProgressEnd;

    public bool IsBusy
    {
        get => isBusy;
        set
        {
            SetProperty(ref isBusy, value);
            Loaded = !isBusy;
            // Update progress bar opacity
            UpdateProgressBarVisibility();
        }
    }

    /// <summary>
    /// Gets the overlay visibility from application state.
    /// Always returns false - overlay is not used on home page, only progress bar is used.
    /// </summary>
    public bool IsHomePageOverlayVisible => false;

    /// <summary>
    /// Indicates if schedules are currently being loaded.
    /// Shows progress bar when schedules are loading (IsBusy is true and Schedules is empty or null).
    /// </summary>
    public bool IsLoadingSchedules
    {
        get
        {
            var isLoading = shouldShowProgressBar && (IsBusy || (Schedules == null || Schedules.Count == 0));
            return isLoading;
        }
    }

    /// <summary>
    /// Opacity for the progress bar. Uses opacity instead of IsVisible to keep control in visual tree
    /// and prevent animation reset during collection rendering.
    /// </summary>
    public double ProgressBarOpacity
    {
        get => progressBarOpacity;
        private set
        {
            if (SetProperty(ref progressBarOpacity, value))
            {
                OnPropertyChanged(nameof(IsProgressBarHidden));
            }
        }
    }

    /// <summary>
    /// Indicates if progress bar is hidden (opacity 0) to make it input transparent.
    /// </summary>
    public bool IsProgressBarHidden => progressBarOpacity == 0;

    private void UpdateProgressBarVisibility()
    {
        var shouldShow = shouldShowProgressBar && (IsBusy || (Schedules == null || Schedules.Count == 0));
        ProgressBarOpacity = shouldShow ? 1.0 : 0.0;

        // Start/stop progress animation based on visibility
        if (shouldShow)
        {
            StartProgressAnimation();
        }
        else
        {
            StopProgressAnimation();
        }
    }

    /// <summary>
    /// Width of the animated range as a percentage (0.0 to 1.0)
    /// </summary>
    public double AnimatedProgressRangeWidth => RangeWidth;

    private void StartProgressAnimation()
    {
        if (progressAnimationTimer != null)
        {
            return; // Already animating
        }

        // Reset to start position
        AnimatedProgressStart = 0.0;
        AnimatedProgressEnd = RangeWidth;

        // Animate a range segment moving from left to right
        progressAnimationTimer = new System.Timers.Timer(20); // Update every 20ms for smoother, faster animation
        progressAnimationTimer.Elapsed += (sender, e) =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // Move the range forward
                AnimatedProgressStart += 0.04; // Move 4% per frame (faster)
                AnimatedProgressEnd = AnimatedProgressStart + RangeWidth;

                // When range reaches the end, reset to start
                if (AnimatedProgressStart >= 1.0)
                {
                    AnimatedProgressStart = 0.0;
                    AnimatedProgressEnd = RangeWidth;
                }

                // Notify both properties changed
                OnPropertyChanged(nameof(AnimatedProgressStart));
                OnPropertyChanged(nameof(AnimatedProgressEnd));
                OnPropertyChanged(nameof(AnimatedProgress));
                OnPropertyChanged(nameof(AnimatedProgressRangeWidth));
            });
        };
        progressAnimationTimer.AutoReset = true;
        progressAnimationTimer.Start();
    }

    private void StopProgressAnimation()
    {
        if (progressAnimationTimer != null)
        {
            progressAnimationTimer.Stop();
            progressAnimationTimer.Dispose();
            progressAnimationTimer = null;
            AnimatedProgressStart = 0.0;
            AnimatedProgressEnd = RangeWidth;
            OnPropertyChanged(nameof(AnimatedProgressStart));
            OnPropertyChanged(nameof(AnimatedProgressEnd));
            OnPropertyChanged(nameof(AnimatedProgress));
        }
    }

    private async Task FadeOutProgressBarAsync()
    {
        // Fade out progress bar smoothly over 200ms to prevent animation reset
        shouldShowProgressBar = false;

        const int fadeSteps = 10;
        const int fadeDurationMs = 200;
        const double stepDelay = fadeDurationMs / (double)fadeSteps;
        const double opacityStep = 1.0 / fadeSteps;

        for (int i = fadeSteps; i >= 0; i--)
        {
            ProgressBarOpacity = i * opacityStep;
            await Task.Delay((int)stepDelay);
        }

        // Ensure it's fully hidden
        ProgressBarOpacity = 0.0;
    }

    /// <summary>
    /// Indicates if bootstrap is complete and databases are ready.
    /// Available for debugging/logging purposes.
    /// </summary>
    public bool IsBootstrapComplete => Common.Helpers.BootstrapHelper.IsBootstrapCompleted();

    /// <summary>
    /// Hides the Schedule page overlay. Called when navigating back to Home page.
    /// </summary>
    public void HideSchedulePageOverlay() => dispatcher.Dispatch(new SetSchedulePageOverlayAction { IsVisible = false });

    /// <summary>
    /// Resets all schedule-related state when navigating back to home.
    /// This ensures only one schedule is in state at any time.
    /// </summary>
    public void ResetScheduleState() => dispatcher.Dispatch(new ResetScheduleStateAction());


    private bool loaded;

    public bool Loaded
    {
        get => loaded;
        set => SetProperty(ref loaded, value);
    }

    public ICommand AddScheduleCommand { get; set; }
    public ICommand ViewScheduleCommand { get; set; }

    private ScheduleViewModel? selectedSchedule;

    public ScheduleViewModel? SelectedSchedule
    {
        get => selectedSchedule;
        set => SetProperty(ref selectedSchedule, value);
    }


    private void OnStateChanged(object? sender, EventArgs e)
    {
        var stateValue = state.Value;

        _ = MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (stateValue.Schedules != null)
            {
                // Check if Schedules collection has actually changed
                var currentScheduleIds = new HashSet<int>(stateValue.Schedules.Where(s => s.Id > 0).Select(s => s.Id));
                var schedulesCountChanged = lastProcessedSchedulesCount != stateValue.Schedules.Count;
                var scheduleIdsChanged = lastProcessedScheduleIds == null || !lastProcessedScheduleIds.SetEquals(currentScheduleIds);

                // Only process if the Schedules collection has changed (not just CurrentSchedule)
                if (!schedulesCountChanged && !scheduleIdsChanged && lastProcessedScheduleIds != null)
                {
                    logger.Debug("OnStateChanged: Skipping processing - Schedules collection unchanged. Count: {Count}", stateValue.Schedules.Count);
                    return;
                }

                logger.Debug("OnStateChanged: Processing {Count} schedules from state. Current Schedules count: {CurrentCount}",
                    stateValue.Schedules.Count, Schedules?.Count ?? 0);

                var hadSchedules = Schedules != null && Schedules.Count > 0;
                var previousScheduleCount = Schedules?.Count ?? 0;
                var currentScheduleCount = stateValue.Schedules?.Count ?? 0;

                // If we had schedules but now don't (cleared), reset progress bar flag
                if (hadSchedules && (stateValue.Schedules == null || stateValue.Schedules.Count == 0))
                {
                    shouldShowProgressBar = true;
                    IsBusy = true;
                    UpdateProgressBarVisibility();
                }

                // Prepare data structures off UI thread (IDs, filtering, and mapping)
                // This moves CPU-intensive operations off the UI thread:
                // - List snapshot creation
                // - Filtering invalid schedules  
                // - AutoMapper mapping (ScheduleStateItem -> AlarmSchedule)
                var (scheduleDataMap, scheduleStateItemMap) = await Task.Run(() =>
                {
                    return scheduleDataPreparer.PrepareScheduleDataOffUIThread(stateValue.Schedules);
                });

                // Prepare ViewModels and collection on UI thread
                var (schedulesToAdd, schedulesToRemove, newSchedules) = scheduleViewModelManager.PrepareScheduleViewModelsOnUIThread(
                    scheduleDataMap,
                    scheduleStateItemMap,
                    Schedules ?? new ObservableHashSet<ScheduleListItemViewModel>());
                var hasSchedulesNow = newSchedules != null && newSchedules.Count > 0;

                // Show progress bar when delete is detected (schedule count decreased)
                if (schedulesToRemove.Count > 0 && previousScheduleCount > currentScheduleCount)
                {
                    logger.Debug("OnStateChanged: Delete detected - showing progress bar. Removing {Count} schedules", schedulesToRemove.Count);
                    shouldShowProgressBar = true;
                    IsBusy = true;
                    UpdateProgressBarVisibility();
                }

                logger.Debug("OnStateChanged: Prepared {AddCount} to add, {RemoveCount} to remove, {NewCount} total. HasSchedulesNow: {HasSchedules}",
                    schedulesToAdd.Count, schedulesToRemove.Count, newSchedules?.Count ?? 0, hasSchedulesNow);

                // Yield multiple times before updating collection to let progress bar animate
                // This gives the UI thread time to process animation frames
                for (int i = 0; i < 3; i++)
                {
                    await Task.Yield();
                    await Task.Delay(30);
                }

                // Update collection incrementally instead of replacing entire collection
                // This reduces rendering overhead and allows progress bar to continue animating
                // On initial load, clear and add items instead of replacing to avoid Windows binding issues
                var isInitialLoad = Schedules == null || Schedules.Count == 0;

                if (isInitialLoad && hasSchedulesNow)
                {
                    // On initial load, we need to ensure the CollectionView properly detects the items
                    // On Windows, CollectionView may not properly handle ObservableHashSet CollectionChanged events
                    // So we'll set the entire collection at once via the property setter
                    logger.Debug("OnStateChanged: Initial load - setting {Count} schedules via property setter", newSchedules.Count);

                    // Wait to ensure CollectionView is fully initialized on Windows
                    await Task.Delay(300);

                    // Set the entire collection via property setter
                    // This ensures the CollectionView binding is properly updated on Windows
                    // We create a new ObservableHashSet with all items and set it
                    var schedulesCollection = new ObservableHashSet<ScheduleListItemViewModel>();
                    foreach (var item in newSchedules)
                    {
                        logger.Debug("OnStateChanged: Adding schedule {ScheduleId} ({Name}) to new collection",
                            item.ScheduleId, item.Name);
                        schedulesCollection.Add(item);
                    }

                    // Set the property - this will trigger SetProperty and notify the UI
                    Schedules = schedulesCollection;
                    logger.Debug("OnStateChanged: Initial load complete. Collection now has {Count} items", Schedules.Count);

                    // Additional delay to let CollectionView finish rendering
                    await Task.Delay(200);
                }
                else if (schedulesToAdd.Count > 0 || schedulesToRemove.Count > 0)
                {
                    // On Windows, CollectionView may not properly detect incremental additions/removals to ObservableHashSet
                    // So we'll create a new collection and replace the entire property to ensure UI updates
                    logger.Debug("OnStateChanged: Updating collection - Adding {AddCount}, Removing {RemoveCount}",
                        schedulesToAdd.Count, schedulesToRemove.Count);

                    var updatedCollection = new ObservableHashSet<ScheduleListItemViewModel>();

                    // Add all existing items that aren't being removed
                    foreach (var existingItem in Schedules)
                    {
                        if (!schedulesToRemove.Contains(existingItem.ScheduleId))
                        {
                            updatedCollection.Add(existingItem);
                        }
                    }

                    // Add new items
                    foreach (var item in schedulesToAdd)
                    {
                        logger.Debug("OnStateChanged: Adding schedule {ScheduleId} ({Name}) to collection",
                            item.ScheduleId, item.Name);
                        updatedCollection.Add(item);
                    }

                    // Replace the entire collection to ensure Windows CollectionView detects the change
                    Schedules = updatedCollection;
                    logger.Debug("OnStateChanged: Collection updated. Now has {Count} items", Schedules.Count);

                    // Hide progress bar after delete operation completes (if it was shown)
                    if (schedulesToRemove.Count > 0)
                    {
                        // Wait a bit to ensure the UI has updated
                        await Task.Delay(200);
                        IsBusy = false;
                        await FadeOutProgressBarAsync();
                    }
                }
                else
                {
                    // No collection change, but still update existing items
                    scheduleViewModelManager.UpdateScheduleViewModels(stateValue.Schedules, navigationHelper.TrackPlayClick);

                    // Hide progress bar if delete was rolled back (schedule count increased back)
                    if (schedulesToAdd.Count > 0 && previousScheduleCount < currentScheduleCount)
                    {
                        logger.Debug("OnStateChanged: Delete rollback detected - hiding progress bar");
                        await Task.Delay(200);
                        IsBusy = false;
                        await FadeOutProgressBarAsync();
                    }
                }

                // Yield a few more times to let CollectionView finish rendering
                for (int i = 0; i < 3; i++)
                {
                    await Task.Yield();
                    await Task.Delay(30);
                }

                // Only set IsBusy to false if we actually have schedules now
                if (hasSchedulesNow)
                {
                    // One final delay to ensure CollectionView has finished initial rendering
                    await Task.Delay(100);
                    IsBusy = false;

                    // Keep progress bar at full opacity during collection rendering
                    // This ensures animation continues smoothly without reset
                    // Wait for CollectionView to finish rendering all items
                    await Task.Delay(400);

                    // Now fade out progress bar smoothly
                    // Keep control in visual tree (using opacity) to prevent animation reset
                    await FadeOutProgressBarAsync();
                }

                // Update last processed state after successful processing
                lastProcessedSchedulesCount = stateValue.Schedules.Count;
                lastProcessedScheduleIds = currentScheduleIds;
            }
            else
            {
                // State doesn't have schedules yet - ensure we show loading state
                logger.Debug("OnStateChanged: State.Schedules is null, showing loading state");
                UpdateProgressBarVisibility();
                
                // Reset tracking when schedules are cleared
                lastProcessedSchedulesCount = null;
                lastProcessedScheduleIds = null;
            }
        });
    }


    public void Dispose()
    {
        state.StateChanged -= OnStateChanged;

        // Stop and dispose progress animation timer
        StopProgressAnimation();

        scheduleViewModelManager.DisposeAll();

    }
}
