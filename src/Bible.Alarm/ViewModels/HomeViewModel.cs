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

    private readonly Dictionary<int, ScheduleListItem> scheduleViewModels = [];

    private readonly Func<int, ScheduleListItem> scheduleListItemFactory;
    private readonly IMapper mapper;

    private readonly IDispatcher dispatcher;
    private readonly IState<ApplicationState> state;
    private readonly IState<PlaybackState> playbackState;
    private readonly INavigationService navigationService;

    // Track recent play button clicks to prevent navigation race condition
    private readonly Dictionary<int, DateTime> recentPlayClicks = new();
    private const int PlayClickCooldownMs = 500; // 500ms cooldown after play click

    public HomeViewModel(
        ILogger logger,
        IServiceScopeFactory scopeFactory,
        Func<int, ScheduleListItem> scheduleListItemFactory,
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

        AddScheduleCommand = new AsyncRelayCommand(async () =>
        {
            // Reset schedule state before creating a new schedule
            // This ensures only one schedule is in state at any time
            dispatcher.Dispatch(new ResetScheduleStateAction());
            await navigationService.NavigateToScheduleAsync();
            dispatcher.Dispatch(new ViewScheduleAction(null));
        });

        ViewScheduleCommand = new AsyncRelayCommand<ScheduleListItem>(async x =>
        {
            if (x == null || x.Schedule == null)
            {
                return;
            }

            if (ShouldSkipNavigation(x.Schedule.Id))
            {
                return;
            }

            await ShowOverlayAndNavigateAsync(x);
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


    private ObservableHashSet<ScheduleListItem> schedules = [];

    public ObservableHashSet<ScheduleListItem> Schedules
    {
        get => schedules;
        set
        {
            SetProperty(ref schedules, value);
            // Update progress bar visibility when schedules collection changes
            UpdateProgressBarVisibility();
        }
    }

    private (List<ScheduleListItem> schedulesToAdd, List<int> schedulesToRemove, ObservableHashSet<ScheduleListItem> newSchedules) PrepareScheduleViewModels(ObservableHashSet<ScheduleStateItem> scheduleItems)
    {
        var currentViewModelIds = new HashSet<int>();
        var schedulesToAdd = new List<ScheduleListItem>();
        var schedulesToRemove = new List<int>();

        // Initialize Schedules if null
        Schedules ??= [];

        // Create a snapshot of the schedule items collection to avoid "Collection was modified" exception
        // This can happen when state changes occur rapidly (e.g., track transitions)
        var scheduleItemsSnapshot = scheduleItems.ToList();

        // Process each schedule item from the snapshot
        // Filter out unsaved schedules (ID <= 0) - these should not appear in the home list
        foreach (var scheduleItem in scheduleItemsSnapshot)
        {
            var scheduleId = scheduleItem.Id;

            // Skip unsaved schedules (ID <= 0) - they should not appear in the home list
            if (scheduleId <= 0)
            {
                continue;
            }

            currentViewModelIds.Add(scheduleId);

            if (scheduleViewModels.TryGetValue(scheduleId, out var existingViewModel))
            {
                // Existing view model - update it from state
                // SetScheduleId() will trigger property change notifications for this specific item
                existingViewModel.SetScheduleId(scheduleId);
                // Ensure callbacks are set (in case it was created before we added this logic)
                if (existingViewModel.OnPlayStarted == null)
                {
                    existingViewModel.OnPlayStarted = () =>
                    {
                        // Track play button click to prevent navigation
                        recentPlayClicks[scheduleId] = DateTime.UtcNow;
                    };
                }
                if (existingViewModel.OnPlaybackStarted == null)
                {
                    existingViewModel.OnPlaybackStarted = () =>
                    {
                        // Playback started - no action needed
                    };
                }
            }
            else
            {
                // New schedule - create new view model using schedule ID
                // ScheduleListItem will initialize from state using the ID
                logger.Debug("PrepareScheduleViewModels: Creating new ScheduleListItem for schedule {ScheduleId}", scheduleId);
                var viewModel = scheduleListItemFactory(scheduleId);
                
                // Verify the view model was initialized correctly
                if (viewModel.Schedule == null)
                {
                    logger.Warning("PrepareScheduleViewModels: ScheduleListItem for schedule {ScheduleId} was not initialized properly (Schedule is null)", scheduleId);
                }
                else
                {
                    logger.Debug("PrepareScheduleViewModels: ScheduleListItem for schedule {ScheduleId} initialized successfully with name '{Name}'", 
                        scheduleId, viewModel.Schedule.Name);
                }
                
                // Set callbacks when play is pressed/started
                viewModel.OnPlayStarted = () =>
                {
                    // Track play button click to prevent navigation
                    recentPlayClicks[scheduleId] = DateTime.UtcNow;
                };
                viewModel.OnPlaybackStarted = () =>
                {
                    // Playback started - no action needed
                };
                scheduleViewModels[scheduleId] = viewModel;
                schedulesToAdd.Add(viewModel);
            }
        }

        // Identify view models to remove
        var toRemove = scheduleViewModels.Keys.Where(id => !currentViewModelIds.Contains(id)).ToList();
        foreach (var id in toRemove)
        {
            if (!scheduleViewModels.TryGetValue(id, out var viewModel))
            {
                continue;
            }

            schedulesToRemove.Add(id);
            viewModel.Dispose();
            scheduleViewModels.Remove(id);
        }

        // Prepare new collection
        var newSchedules = new ObservableHashSet<ScheduleListItem>();

        // Create a snapshot of current Schedules to avoid "Collection was modified" exception
        var currentSchedulesSnapshot = Schedules.ToList();

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

    private void UpdateScheduleViewModels(ObservableHashSet<ScheduleStateItem> scheduleItems)
    {
        var currentViewModelIds = new HashSet<int>();

        // Initialize Schedules if null
        Schedules ??= [];

        // Create a snapshot of the schedule items collection to avoid "Collection was modified" exception
        var scheduleItemsSnapshot = scheduleItems.ToList();

        // Process each schedule item from the snapshot
        foreach (var scheduleItem in scheduleItemsSnapshot)
        {
            var scheduleId = scheduleItem.Id;

            if (scheduleId <= 0)
            {
                continue;
            }

            currentViewModelIds.Add(scheduleId);

            if (scheduleViewModels.TryGetValue(scheduleId, out var existingViewModel))
            {
                // Existing view model - update it from state
                existingViewModel.SetScheduleId(scheduleId);
                // Ensure callbacks are set
                if (existingViewModel.OnPlayStarted == null)
                {
                    existingViewModel.OnPlayStarted = () =>
                    {
                        recentPlayClicks[scheduleId] = DateTime.UtcNow;
                    };
                }
                if (existingViewModel.OnPlaybackStarted == null)
                {
                    existingViewModel.OnPlaybackStarted = () =>
                    {
                        // Playback started - no action needed
                    };
                }
            }
        }
        // For updates only, we don't change the collection - just update existing items
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
                
                // Prepare view models but don't update collection yet
                // This allows progress bar to continue animating while we prepare data
                var (schedulesToAdd, schedulesToRemove, newSchedules) = PrepareScheduleViewModels(stateValue.Schedules);
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
                    var schedulesCollection = new ObservableHashSet<ScheduleListItem>();
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
                    
                    var updatedCollection = new ObservableHashSet<ScheduleListItem>();
                    
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
                    UpdateScheduleViewModels(stateValue.Schedules);
                    
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
            }
            else
            {
                // State doesn't have schedules yet - ensure we show loading state
                logger.Debug("OnStateChanged: State.Schedules is null, showing loading state");
                UpdateProgressBarVisibility();
            }
        });
    }

    private bool ShouldSkipNavigation(int scheduleId)
    {
        var currentPlaybackState = playbackState.Value;
        if (currentPlaybackState.IsPreparingOrPlaying &&
            currentPlaybackState.CurrentScheduleId == scheduleId)
        {
            logger.Debug("ViewScheduleCommand: Skipping navigation - playback is active for schedule {ScheduleId}", scheduleId);
            return true;
        }

        if (recentPlayClicks.TryGetValue(scheduleId, out var playClickTime))
        {
            var timeSincePlayClick = (DateTime.UtcNow - playClickTime).TotalMilliseconds;
            if (timeSincePlayClick < PlayClickCooldownMs)
            {
                logger.Debug("ViewScheduleCommand: Skipping navigation - play button was clicked {TimeSinceClick}ms ago for schedule {ScheduleId}",
                    timeSincePlayClick, scheduleId);
                return true;
            }
            recentPlayClicks.Remove(scheduleId);
        }

        return false;
    }

    private async Task ShowOverlayAndNavigateAsync(ScheduleListItem scheduleListItem)
    {
        if (scheduleListItem.Schedule == null)
        {
            return;
        }

        scheduleListItem.Schedule.IsEnabled = scheduleListItem.IsEnabled;
        var navigationTask = navigationService.NavigateToScheduleAsync();
        var scheduleStateItem = GetScheduleStateItem(scheduleListItem.Schedule.Id);
        dispatcher.Dispatch(new ViewScheduleAction(scheduleStateItem));
        await navigationTask;
    }

    private ScheduleStateItem GetScheduleStateItem(int scheduleId)
    {
        var scheduleStateItem = state.Value.Schedules?.FirstOrDefault(s => s.Id == scheduleId);
        if (scheduleStateItem == null)
        {
            return mapper.Map<ScheduleStateItem>(scheduleViewModels[scheduleId].Schedule);
        }
        return scheduleStateItem.DeepClone();
    }

    public void Dispose()
    {
        state.StateChanged -= OnStateChanged;

        // Stop and dispose progress animation timer
        StopProgressAnimation();

        if (Schedules != null)
        {
            foreach (var item in Schedules)
            {
                item.Dispose();
            }
        }

    }
}
