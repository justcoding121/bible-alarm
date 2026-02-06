#nullable enable

using System.Collections.ObjectModel;
using System.Windows.Input;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.Shared;
using Bible.Alarm.ViewModels.ScheduleViewModelHelpers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;
#if ANDROID
using Bible.Alarm.Platforms.Android.Services.Helpers;
#endif

namespace Bible.Alarm.ViewModels.Schedule;

public sealed class NumberOfTrackContainerViewModel : ObservableObject, IDisposable
{
    private readonly ILogger logger;
    private readonly INavigationService navigationService;
    private readonly IServiceProvider serviceProvider;
    private readonly IState<ApplicationState> state;
    private readonly IDispatcher dispatcher;

    private int scheduleId;
    private bool notificationEnabled;
    private bool alwaysPlayFromStart;
    private bool playIndefinitely;
    private bool isProcessingStateChange;
    private string? lastCategoryName;
#if ANDROID
    private CancellationTokenSource? permissionCheckCancellationTokenSource;
    private bool isUpdatingFromPermissionCheck;
    private bool isPermissionCheckTaskRunning;
    private bool isSyncingFromState;
#endif
    private readonly ContainerReadySignaler containerReadySignaler;

    private ObservableCollection<NumberOfTracksListViewItemModel> numberOfTracksList = new();
    private NumberOfTracksListViewItemModel? currentNumberOfTracks;
    private bool isBusy = true;

    public bool IsBusy
    {
        get => isBusy;
        set => SetProperty(ref isBusy, value);
    }

    public NumberOfTrackContainerViewModel(
        ILogger logger,
        INavigationService navigationService,
        IServiceProvider serviceProvider,
        IState<ApplicationState> state,
        IDispatcher dispatcher)
    {
        this.logger = logger;
        this.navigationService = navigationService;
        this.serviceProvider = serviceProvider;
        this.state = state;
        this.dispatcher = dispatcher;
        containerReadySignaler = new ContainerReadySignaler(state, dispatcher, "NumberOfTrack", s => s.ContainerReadiness.NumberOfTrack);

        state.StateChanged += OnStateChanged;
        InitializeCommands();
        InitializeFromState();
    }

    private void InitializeCommands()
    {
        OpenModalCommand = new AsyncRelayCommand(async () =>
        {
            await navigationService.OpenNumberOfTracksModalAsync(this);
        });

        SelectNumberOfTracksCommand = new AsyncRelayCommand<NumberOfTracksListViewItemModel>(async x =>
        {
            if (CurrentNumberOfTracks != null)
            {
                CurrentNumberOfTracks.IsSelected = false;
            }

            CurrentNumberOfTracks = x;
            if (CurrentNumberOfTracks != null)
            {
                CurrentNumberOfTracks.IsSelected = true;
            }

            // Dispatch update to state
            if (CurrentNumberOfTracks != null)
            {
                DispatchScheduleUpdate(s => s.NumberOfTracksToPlay = CurrentNumberOfTracks.Value);
            }

            // Explicitly notify property changes to ensure UI binding updates
            OnPropertyChanged(nameof(CurrentNumberOfTracks));
            OnPropertyChanged(nameof(CurrentNumberOfTracksText));

            await navigationService.PopModalAsync();
        });

        ToggleAlwaysPlayFromStartCommand = new RelayCommand(() => AlwaysPlayFromStart = !AlwaysPlayFromStart);

        TogglePlayIndefinitelyCommand = new RelayCommand(() => PlayIndefinitely = !PlayIndefinitely);

        NotificationEnabledCommand = new RelayCommand(() => { NotificationEnabled = !NotificationEnabled; });

        CloseModalCommand = new AsyncRelayCommand(async () =>
        {
            await navigationService.PopModalAsync();
        });
    }

    private void InitializeFromState()
    {
        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule != null)
        {
            scheduleId = currentSchedule.Id;
            notificationEnabled = currentSchedule.NotificationEnabled;
            alwaysPlayFromStart = currentSchedule.AlwaysPlayFromStart;
            playIndefinitely = currentSchedule.NumberOfTracksToPlay <= 0;
            lastCategoryName = currentSchedule.BiblePublicationCategoryName;

            PopulateNumberOfTracksListView();

            OnPropertyChanged(nameof(NotificationEnabled));
            OnPropertyChanged(nameof(AlwaysPlayFromStart));
            OnPropertyChanged(nameof(PlayIndefinitely));
            OnPropertyChanged(nameof(IsNumberOfTracksSelectionVisible));
            OnPropertyChanged(nameof(TrackLabelText));
            OnPropertyChanged(nameof(ModalHeaderText));
            OnPropertyChanged(nameof(RestartLabelText));
            OnPropertyChanged(nameof(SelectedTracksText));

            // Signal that this container is ready (initialized from CurrentSchedule)
            containerReadySignaler.TrySignalReady();
        }
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        // Prevent re-entrant calls to avoid cycles
        if (isProcessingStateChange)
        {
            return;
        }

        isProcessingStateChange = true;
        try
        {
            var stateValue = state.Value;
            var currentSchedule = stateValue.CurrentSchedule;

            // If ContainerReadiness was reset to NotReady but we've already signaled ready, reset our flag
            // This handles the case where ViewScheduleAction resets ContainerReadiness after containers signaled ready
            if (containerReadySignaler.HasSignaledReady && !stateValue.ContainerReadiness.NumberOfTrack && currentSchedule != null)
            {
                containerReadySignaler.Reset();
                // Re-initialize and signal ready again
                InitializeFromState();
                return;
            }

            // If we don't have a scheduleId yet (initial state), initialize when CurrentSchedule is set
            // But only if we haven't already signaled ready (prevents infinite loop for new schedules with Id=0)
            if (scheduleId == 0 && currentSchedule != null && !containerReadySignaler.HasSignaledReady)
            {
                InitializeFromState();
                return;
            }

            // Initialize if schedule ID changed to a different positive ID (existing schedule opened)
            if (currentSchedule != null && currentSchedule.Id != scheduleId && currentSchedule.Id > 0)
            {
                containerReadySignaler.Reset();
                InitializeFromState();
            }
            else if (currentSchedule != null)
            {
                // Update properties if schedule changed
                // Don't sync NotificationEnabled if permission check task is running
                // This prevents OnStateChanged from overriding permission-based updates
                if (!isPermissionCheckTaskRunning && notificationEnabled != currentSchedule.NotificationEnabled)
                {
                    isSyncingFromState = true;
                    try
                    {
                        notificationEnabled = currentSchedule.NotificationEnabled;
                        OnPropertyChanged(nameof(NotificationEnabled));
                    }
                    finally
                    {
                        isSyncingFromState = false;
                    }
                }
                if (alwaysPlayFromStart != currentSchedule.AlwaysPlayFromStart)
                {
                    alwaysPlayFromStart = currentSchedule.AlwaysPlayFromStart;
                    OnPropertyChanged(nameof(AlwaysPlayFromStart));
                }
                var newPlayIndefinitely = currentSchedule.NumberOfTracksToPlay <= 0;
                if (playIndefinitely != newPlayIndefinitely)
                {
                    playIndefinitely = newPlayIndefinitely;
                    OnPropertyChanged(nameof(PlayIndefinitely));
                    OnPropertyChanged(nameof(IsNumberOfTracksSelectionVisible));
                }

                // Check if category changed (Bible/Dramas/Music -> affects UI wording)
                var newCategoryName = currentSchedule.BiblePublicationCategoryName;
                if (!string.Equals(lastCategoryName, newCategoryName, StringComparison.OrdinalIgnoreCase))
                {
                    OnPropertyChanged(nameof(TrackLabelText));
                    OnPropertyChanged(nameof(TracksLabelText));
                    OnPropertyChanged(nameof(SelectedTracksText));
                    OnPropertyChanged(nameof(ModalHeaderText));
                    OnPropertyChanged(nameof(RestartLabelText));

                    // Default selection is always 1 (tracks/episodes/chapters).
                    const int newDefault = 1;

                    // Repopulate the list (unit labels may have changed)
                    PopulateNumberOfTracksListView(newDefault);

                    // Only update NumberOfTracksToPlay if we're in finite mode.
                    if (!playIndefinitely)
                    {
                        DispatchScheduleUpdate(s => s.NumberOfTracksToPlay = newDefault);
                    }
                }
                lastCategoryName = newCategoryName;
            }
        }
        finally
        {
            isProcessingStateChange = false;
        }
    }

    public ICommand OpenModalCommand { get; private set; } = null!;
    public ICommand SelectNumberOfTracksCommand { get; private set; } = null!;
    public ICommand ToggleAlwaysPlayFromStartCommand { get; private set; } = null!;
    public ICommand TogglePlayIndefinitelyCommand { get; private set; } = null!;
    public ICommand NotificationEnabledCommand { get; private set; } = null!;
    public ICommand CloseModalCommand { get; private set; } = null!;

    public ObservableCollection<NumberOfTracksListViewItemModel> NumberOfTracksList
    {
        get => numberOfTracksList;
        set => SetProperty(ref numberOfTracksList, value);
    }

    public NumberOfTracksListViewItemModel? CurrentNumberOfTracks
    {
        get => currentNumberOfTracks;
        set
        {
            if (SetProperty(ref currentNumberOfTracks, value))
            {
                // Notify that the Text property (computed from CurrentNumberOfTracks) has changed
                OnPropertyChanged(nameof(CurrentNumberOfTracksText));
                OnPropertyChanged(nameof(SelectedTracksText));
                OnPropertyChanged(nameof(SelectedNumberText));
            }
        }
    }

    /// <summary>
    /// Computed property for binding to the number of tracks text in the UI.
    /// This ensures the UI updates when CurrentNumberOfTracks changes.
    /// </summary>
    public string CurrentNumberOfTracksText => CurrentNumberOfTracks?.Text ?? string.Empty;

    private enum TracksUnit
    {
        Chapter,
        Episode,
        Track
    }

    private TracksUnit GetTracksUnit()
    {
        var categoryName = state.Value.CurrentSchedule?.BiblePublicationCategoryName;

        if (string.Equals(categoryName, "Music", StringComparison.OrdinalIgnoreCase))
        {
            return TracksUnit.Track;
        }

        if (string.Equals(categoryName, "Dramas", StringComparison.OrdinalIgnoreCase))
        {
            return TracksUnit.Episode;
        }

        // Default wording matches the Bible category (and any future categories that behave like Bible).
        return TracksUnit.Chapter;
    }

    private (string Singular, string Plural) GetUnitTextTitleCase()
    {
        return GetTracksUnit() switch
        {
            TracksUnit.Track => ("Track", "Tracks"),
            TracksUnit.Episode => ("Episode", "Episodes"),
            _ => ("Chapter", "Chapters")
        };
    }

    private (string Singular, string Plural) GetUnitTextLowerCase()
    {
        return GetTracksUnit() switch
        {
            TracksUnit.Track => ("track", "tracks"),
            TracksUnit.Episode => ("episode", "episodes"),
            _ => ("chapter", "chapters")
        };
    }

    /// <summary>
    /// Gets the label text for the tracks selection row.
    /// Uses category-based wording:
    /// - Music => Tracks
    /// - Dramas => Episodes
    /// - Others => Chapters
    /// </summary>
    public string TrackLabelText
    {
        get
        {
            var (_, titlePlural) = GetUnitTextTitleCase();
            return $"{titlePlural} to play each time";
        }
    }

    /// <summary>
    /// Gets the static label text for the tracks selection row.
    /// Uses category-based wording:
    /// - Music => Tracks
    /// - Dramas => Episodes
    /// - Others => Chapters
    /// </summary>
    public string TracksLabelText
    {
        get
        {
            var (_, plural) = GetUnitTextLowerCase();
            return $"Number of {plural} to play";
        }
    }

    /// <summary>
    /// Gets the dynamic selected value text showing the number with proper singular/plural.
    /// Returns format like "3 Chapters", "1 Chapter", "3 Episodes", "1 Episode", "3 Tracks", or "1 Track".
    /// </summary>
    public string SelectedTracksText
    {
        get
        {
            var number = CurrentNumberOfTracks?.Value ?? 0;
            if (number == 0)
            {
                var (_, titlePlural) = GetUnitTextTitleCase();
                return titlePlural;
            }

            var (titleSingular, titlePlural2) = GetUnitTextTitleCase();
            var selectedUnit = number == 1 ? titleSingular : titlePlural2;
            return $"{number} {selectedUnit}";
        }
    }

    /// <summary>
    /// Gets just the number value as a string for display in the container.
    /// Returns format like "3" or "1".
    /// </summary>
    public string SelectedNumberText
    {
        get
        {
            var number = CurrentNumberOfTracks?.Value ?? 0;
            return number.ToString();
        }
    }

    /// <summary>
    /// Gets the header text for the tracks selection modal.
    /// Uses category-based wording:
    /// - Music => Tracks
    /// - Dramas => Episodes
    /// - Others => Chapters
    /// </summary>
    public string ModalHeaderText
    {
        get
        {
            var (_, plural) = GetUnitTextTitleCase();
            return $"Select Number of {plural}";
        }
    }

    /// <summary>
    /// Gets the label text for the "restart incomplete" toggle.
    /// Uses category-based wording:
    /// - Music => tracks
    /// - Dramas => episodes
    /// - Others => chapters
    /// </summary>
    public string RestartLabelText
    {
        get
        {
            var (_, plural) = GetUnitTextLowerCase();
            return $"Restart incomplete {plural} from the beginning";
        }
    }

    public bool NotificationEnabled
    {
        get => notificationEnabled;
        set
        {
            // Prevent re-entrancy - if we're already processing, ignore
            if (isUpdatingFromPermissionCheck || isSyncingFromState)
            {
                logger.Debug("NotificationEnabled setter called during update/sync - ignoring. isUpdatingFromPermissionCheck={IsUpdating}, isSyncingFromState={IsSyncing}, value={Value}", 
                    isUpdatingFromPermissionCheck, isSyncingFromState, value);
                return;
            }
            
            var isUserAction = !isSyncingFromState;
            
#if ANDROID
            // Only start background task if this is a genuine user action (not from state sync or internal update)
            // If user is trying to toggle ON, update property and start background task
            // The task will adjust the property based on permission status
            if (isUserAction && value)
            {
                logger.Debug("User toggled ON - updating property and starting permission check task");
                
                // Update property to reflect user's intent (UI will show ON optimistically)
                if (SetProperty(ref notificationEnabled, true))
                {
                    DispatchScheduleUpdate(s => s.NotificationEnabled = true);
                }
                
                // Start background task to check permission
                // The task will update the property if permission is denied
                StartPermissionCheckTask();
                return;
            }
            
            // For all other cases (toggling OFF, or internal updates), update immediately
            if (SetProperty(ref notificationEnabled, value))
            {
                DispatchScheduleUpdate(s => s.NotificationEnabled = value);
            }

            // Only stop background task if this is a user action toggling OFF
            if (isUserAction && !value)
            {
                // User toggled OFF - terminate background task
                logger.Debug("User toggled OFF - stopping permission check task");
                StopPermissionCheckTask();
            }
#else
            // Non-Android platforms - update immediately
            if (SetProperty(ref notificationEnabled, value))
            {
                DispatchScheduleUpdate(s => s.NotificationEnabled = value);
            }
#endif
        }
    }

#if ANDROID
    private void StartPermissionCheckTask()
    {
        // Cancel any existing task
        StopPermissionCheckTask();

        // Start background task to poll permission status
        permissionCheckCancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = permissionCheckCancellationTokenSource.Token;
        isPermissionCheckTaskRunning = true;

        // Request permission if needed (one-time) - do this first before starting the polling loop
        _ = Task.Run(async () =>
        {
            try
            {
                await NotificationPermissionHelper.RequestNotificationPermissionIfNeededAsync();
                logger.Debug("Permission request completed");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error requesting notification permission");
            }
        });

        _ = Task.Run(async () =>
        {
            try
            {
                // Wait a bit before first check to allow permission dialog to appear
                await Task.Delay(300, cancellationToken);
                
                while (!cancellationToken.IsCancellationRequested)
                {
                    var granted = NotificationPermissionHelper.IsNotificationPermissionGranted();
                    
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            return;
                        }

                        var currentValue = notificationEnabled;
                        logger.Debug("Permission check: granted={Granted}, currentNotificationEnabled={Current}", granted, currentValue);

                        if (granted)
                        {
                            // Permission granted - toggle ON (internal update, not user action)
                            if (!currentValue)
                            {
                                logger.Information("Notification permission granted - updating toggle to ON. Current value: {Current}", currentValue);
                                isUpdatingFromPermissionCheck = true;
                                try
                                {
                                    // Use SetProperty to ensure proper binding updates
                                    if (SetProperty(ref notificationEnabled, true))
                                    {
                                        DispatchScheduleUpdate(s => s.NotificationEnabled = true);
                                        logger.Debug("Updated NotificationEnabled to true and dispatched state update");
                                    }
                                }
                                finally
                                {
                                    isUpdatingFromPermissionCheck = false;
                                }
                            }
                            else
                            {
                                // Permission granted and toggle already ON - task is no longer needed
                                logger.Debug("Permission granted and NotificationEnabled already true - stopping task");
                                permissionCheckCancellationTokenSource?.Cancel();
                                return;
                            }
                        }
                        else
                        {
                            // Permission denied - toggle OFF (internal update, not user action)
                            if (currentValue)
                            {
                                logger.Information("Notification permission denied - updating toggle to OFF. Current value: {Current}", currentValue);
                                isUpdatingFromPermissionCheck = true;
                                try
                                {
                                    // Use SetProperty to ensure proper binding updates
                                    if (SetProperty(ref notificationEnabled, false))
                                    {
                                        DispatchScheduleUpdate(s => s.NotificationEnabled = false);
                                        logger.Debug("Updated NotificationEnabled to false and dispatched state update");
                                        
                                        // Show toast message to inform user
                                        var toastService = serviceProvider.GetService<IToastService>();
                                        if (toastService != null)
                                        {
                                            _ = toastService.ShowMessage("Notification permission is required for tap-to-play alarms. Please enable notifications in system settings.", 5);
                                        }
                                    }
                                }
                                finally
                                {
                                    isUpdatingFromPermissionCheck = false;
                                }
                            }
                            else
                            {
                                logger.Debug("Permission denied but NotificationEnabled already false - no update needed");
                            }
                        }
                    });
                    
                    // Wait before next check (increased delay to reduce CPU usage)
                    await Task.Delay(1000, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                logger.Debug("Permission check task cancelled");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in permission check task");
            }
            finally
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    isPermissionCheckTaskRunning = false;
                });
            }
        }, cancellationToken);
    }

    private void StopPermissionCheckTask()
    {
        if (permissionCheckCancellationTokenSource != null)
        {
            permissionCheckCancellationTokenSource.Cancel();
            permissionCheckCancellationTokenSource.Dispose();
            permissionCheckCancellationTokenSource = null;
            isPermissionCheckTaskRunning = false;
            logger.Debug("Stopped permission check task");
        }
    }

    public void StopPermissionCheckTaskIfRunning()
    {
        StopPermissionCheckTask();
    }
#endif

    public bool AlwaysPlayFromStart
    {
        get => alwaysPlayFromStart;
        set
        {
            if (SetProperty(ref alwaysPlayFromStart, value))
            {
                DispatchScheduleUpdate(s => s.AlwaysPlayFromStart = value);
            }
        }
    }

    /// <summary>
    /// When enabled, the schedule plays indefinitely (NumberOfTracksToPlay is stored as 0).
    /// When disabled, the user selects a finite number of chapters/episodes to play.
    /// </summary>
    public bool PlayIndefinitely
    {
        get => playIndefinitely;
        set
        {
            if (!SetProperty(ref playIndefinitely, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsNumberOfTracksSelectionVisible));

            if (playIndefinitely)
            {
                // Store 0 to indicate indefinite playback
                DispatchScheduleUpdate(s => s.NumberOfTracksToPlay = 0);
                return;
            }

            // Switching back to finite mode:
            // Ensure a valid selection exists, otherwise apply a sensible default.
            const int defaultTracks = 1;
            var selected = CurrentNumberOfTracks?.Value ?? defaultTracks;
            if (selected <= 0)
            {
                selected = defaultTracks;
            }

            // Ensure UI list has a selection even if schedule previously stored 0.
            PopulateNumberOfTracksListView(selected);
            DispatchScheduleUpdate(s => s.NumberOfTracksToPlay = selected);
        }
    }

    /// <summary>
    /// True when the finite number-of-tracks row should be shown.
    /// </summary>
    public bool IsNumberOfTracksSelectionVisible => !PlayIndefinitely;

    private async void PopulateNumberOfTracksListView(int? forceSelection = null)
    {
        // Preserve the current selection if user has made one, or use forced selection
        var preservedSelection = forceSelection ?? CurrentNumberOfTracks?.Value;
        var currentSchedule = state.Value.CurrentSchedule;
        var (unitSingularLower, unitPluralLower) = GetUnitTextLowerCase();

        // Default selection is always 1 (chapters/episodes).
        const int defaultTracks = 1;
        var numberOfTracksFromSchedule = currentSchedule?.NumberOfTracksToPlay ?? defaultTracks;
        if (numberOfTracksFromSchedule <= 0)
        {
            // Indefinite mode stores 0; keep a valid default selected for when user disables indefinite later.
            numberOfTracksFromSchedule = defaultTracks;
        }
        var numberOfTracks = preservedSelection ?? numberOfTracksFromSchedule;

        // Determine maximum number of tracks to show
        // Must match AlarmSchedule.NumberOfTracksToPlay validation range and modal max
        const int maxTracksCap = 21;
        int maxTracks = maxTracksCap;
        
        // For dramas, get the actual number of episodes (cap to 21).
        if (GetTracksUnit() == TracksUnit.Episode && currentSchedule != null)
        {
            try
            {
                var biblePublicationService = serviceProvider.GetService<Bible.Alarm.Shared.Services.Media.Interfaces.IBiblePublicationService>();
                if (biblePublicationService != null && 
                    !string.IsNullOrEmpty(currentSchedule.BiblePublicationLanguageCode) &&
                    !string.IsNullOrEmpty(currentSchedule.BiblePublicationCode))
                {
                    var publication = await biblePublicationService.GetByLanguageAndCodeWithTracksAsync(
                        currentSchedule.BiblePublicationLanguageCode,
                        currentSchedule.BiblePublicationCode);
                    
                    if (publication?.Tracks != null && publication.Tracks.Count > 0)
                    {
                        // Cap to avoid allowing selections the model can't save/validate.
                        maxTracks = Math.Min(publication.Tracks.Count, maxTracksCap);
                        logger.Debug("PopulateNumberOfTracksListView: Non-sectioned publication has {TrackCount} episodes, setting max to {MaxTracks}",
                            publication.Tracks.Count, maxTracks);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "PopulateNumberOfTracksListView: Failed to get track count for non-sectioned publication, using default max of 21");
            }
        }

        var trackVMs = new ObservableCollection<NumberOfTracksListViewItemModel>();

        for (var i = 1; i <= maxTracks; i++)
        {
            var tracksVm = new NumberOfTracksListViewItemModel(i, unitSingularLower, unitPluralLower);

            // If user has made a selection, use that; otherwise use the state's value
            var shouldSelect = preservedSelection.HasValue
                ? preservedSelection.Value == i
                : numberOfTracks == i;

            if (shouldSelect)
            {
                tracksVm.IsSelected = true;
                CurrentNumberOfTracks = tracksVm;
            }

            trackVMs.Add(tracksVm);
        }

        NumberOfTracksList = trackVMs;
        
        // Notify that the list has been updated (in case selection needs to be reapplied)
        OnPropertyChanged(nameof(NumberOfTracksList));
    }

    private void DispatchScheduleUpdate(Action<ScheduleStateItem> updateAction)
    {
        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule == null)
        {
            return;
        }

        // Clone the current schedule and apply the update
        var updatedSchedule = ScheduleStateHelper.CloneScheduleStateItem(currentSchedule);
        updateAction(updatedSchedule);
        dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(updatedSchedule, false, false, shouldSave: false));
    }

    public void Dispose()
    {
        state.StateChanged -= OnStateChanged;
#if ANDROID
        StopPermissionCheckTask();
#endif
    }
}

