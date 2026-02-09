#nullable enable

using System.Collections.ObjectModel;
using System.Windows.Input;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.General;
using Bible.Alarm.ViewModels.Shared;
using Bible.Alarm.ViewModels.ScheduleViewModelHelpers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Microsoft.Maui.ApplicationModel;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;
#if ANDROID
using Bible.Alarm.Platforms.Android.Services.Helpers;
#elif IOS
using Bible.Alarm.Platforms.iOS.Services.Helpers;
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
    private bool isUpdatingFromPermissionCheck;
    private bool isSyncingFromState;
    private bool isWaitingForPermissionResponse;
    private NotificationPermissionService? permissionService;
#elif IOS
    private bool isUpdatingFromPermissionCheck;
    private bool isSyncingFromState;
    private bool isWaitingForPermissionResponse;
    private IOSNotificationPermissionService? permissionService;
#else
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
    private bool isUpdatingFromPermissionCheck;
    private bool isSyncingFromState;
    private bool isWaitingForPermissionResponse;
    private object? permissionService;
#pragma warning restore CS0649
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
#if ANDROID || IOS
        InitializePermissionService();
        InitializeFromState();
#else
        InitializeFromState();
#endif
    }

#if ANDROID
    private void InitializePermissionService()
    {
        permissionService = NotificationPermissionService.Instance;
        
        // Subscribe to permission events
        permissionService.PermissionGranted += OnPermissionGranted;
        permissionService.PermissionDenied += OnPermissionDenied;
    }
#elif IOS
    private void InitializePermissionService()
    {
        permissionService = IOSNotificationPermissionService.Instance;
        
        // Subscribe to permission events
        permissionService.PermissionGranted += OnPermissionGranted;
        permissionService.PermissionDenied += OnPermissionDenied;
    }
#endif

#if ANDROID || IOS

    private void OnPermissionGranted(object? sender, EventArgs e)
    {
        logger.Information("NotificationPermissionService: Permission granted event received. isWaitingForPermissionResponse={IsWaiting}, currentNotificationEnabled={Current}", 
            isWaitingForPermissionResponse, notificationEnabled);
        
        // Only update if we're waiting for permission response
        if (isWaitingForPermissionResponse)
        {
            isUpdatingFromPermissionCheck = true;
            try
            {
                // Always set toggle to ON when permission is granted
                // Force update to ensure UI reflects the ON state
                var oldValue = notificationEnabled;
                notificationEnabled = true;
                OnPropertyChanged(nameof(NotificationEnabled));
                DispatchScheduleUpdate(s => s.NotificationEnabled = true);
                logger.Information("Set NotificationEnabled to true after permission granted. Old value: {OldValue}, New value: {NewValue}", 
                    oldValue, notificationEnabled);
            }
            finally
            {
                isUpdatingFromPermissionCheck = false;
                isWaitingForPermissionResponse = false;
            }
        }
        else
        {
            logger.Debug("Permission granted event received but not waiting for response - ignoring");
        }
    }

    private void OnPermissionDenied(object? sender, EventArgs e)
    {
        logger.Information("NotificationPermissionService: Permission denied event received");
        
        // Only show toast if we're waiting for permission response
        if (isWaitingForPermissionResponse)
        {
            isUpdatingFromPermissionCheck = true;
            try
            {
                // Always ensure toggle is OFF when permission is denied
                // Force update to ensure UI reflects the OFF state
                notificationEnabled = false;
                OnPropertyChanged(nameof(NotificationEnabled));
                DispatchScheduleUpdate(s => s.NotificationEnabled = false);
                logger.Debug("Set NotificationEnabled to false after permission denied");
                
                // Show notification permission modal instead of toast
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    try
                    {
                        var notificationViewModel = new NotificationPermissionViewModel(
                            logger,
                            navigationService,
                            serviceProvider,
                            onModalDismissed: (permissionGranted) =>
                            {
                                if (permissionGranted)
                                {
                                    MainThread.BeginInvokeOnMainThread(() =>
                                    {
                                        isUpdatingFromPermissionCheck = true;
                                        try
                                        {
                                            // Set NotificationEnabled to ON when permission is granted
                                            notificationEnabled = true;
                                            OnPropertyChanged(nameof(NotificationEnabled));
                                            DispatchScheduleUpdate(s => s.NotificationEnabled = true);
                                            logger.Information("Set NotificationEnabled to true after permission granted from modal");
                                        }
                                        finally
                                        {
                                            isUpdatingFromPermissionCheck = false;
                                        }
                                    });
                                }
                            });
                        
                        notificationViewModel.StartPermissionCheckTimer();
                        await navigationService.OpenNotificationPermissionModalAsync(notificationViewModel);
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, "Error opening notification permission modal after permission denied");
                    }
                });
            }
            finally
            {
                isUpdatingFromPermissionCheck = false;
                isWaitingForPermissionResponse = false;
            }
        }
    }
#endif

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
        try
        {
            var currentSchedule = state.Value.CurrentSchedule;
            if (currentSchedule != null)
            {
                scheduleId = currentSchedule.Id;
                notificationEnabled = currentSchedule.NotificationEnabled;
                alwaysPlayFromStart = currentSchedule.AlwaysPlayFromStart;
                playIndefinitely = currentSchedule.NumberOfTracksToPlay <= 0;
                lastCategoryName = currentSchedule.BiblePublicationCategoryName;

#if ANDROID
                // If NotificationEnabled is true in state but permission is not granted, sync it to OFF silently
                // This handles the case where user revoked permission via Android settings
                // Don't request permission here - just sync the local property to match actual permission status
                // State will be synced in OnStateChanged to avoid interfering with container readiness signaling
                try
                {
                    if (notificationEnabled && permissionService != null && !permissionService.IsGranted)
                    {
                        logger.Information("InitializeFromState: NotificationEnabled is true in state but permission is not granted - setting local property to OFF");
                        notificationEnabled = false;
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "InitializeFromState: Exception checking notification permission - assuming not granted");
                    // If permission check fails, assume not granted and sync to OFF
                    if (notificationEnabled)
                    {
                        notificationEnabled = false;
                    }
                }
#elif IOS
                // If NotificationEnabled is true in state but permission is not granted, sync it to OFF silently
                // This handles the case where user revoked permission via iOS settings
                // Don't request permission here - just sync the local property to match actual permission status
                // State will be synced in OnStateChanged to avoid interfering with container readiness signaling
                try
                {
                    if (notificationEnabled && permissionService != null && !permissionService.IsGranted)
                    {
                        logger.Information("InitializeFromState: NotificationEnabled is true in state but permission is not granted - setting local property to OFF");
                        notificationEnabled = false;
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "InitializeFromState: Exception checking notification permission - assuming not granted");
                    // If permission check fails, assume not granted and sync to OFF
                    if (notificationEnabled)
                    {
                        notificationEnabled = false;
                    }
                }
#endif

            _ = PopulateNumberOfTracksListViewAsync();

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
        catch (Exception ex)
        {
            logger.Error(ex, "InitializeFromState: Exception initializing from state");
            // Don't rethrow - allow app to continue even if initialization fails
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
                if (!isWaitingForPermissionResponse && notificationEnabled != currentSchedule.NotificationEnabled)
                {
                    isSyncingFromState = true;
                    try
                    {
                        var newValue = currentSchedule.NotificationEnabled;
                        
#if ANDROID
                        // If state has NotificationEnabled=true but permission is not granted, sync to OFF
                        // This handles the case where user revoked permission via Android settings
                        try
                        {
                            if (newValue && permissionService != null && !permissionService.IsGranted)
                            {
                                logger.Information("OnStateChanged: NotificationEnabled is true in state but permission is not granted - syncing to OFF");
                                newValue = false;
                                // Update state to reflect actual permission status
                                DispatchScheduleUpdate(s => s.NotificationEnabled = false);
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.Error(ex, "OnStateChanged: Exception checking notification permission - assuming not granted");
                            // If permission check fails, assume not granted and sync to OFF
                            if (newValue)
                            {
                                newValue = false;
                                DispatchScheduleUpdate(s => s.NotificationEnabled = false);
                            }
                        }
#elif IOS
                        // If state has NotificationEnabled=true but permission is not granted, sync to OFF
                        // This handles the case where user revoked permission via iOS settings
                        try
                        {
                            if (newValue && permissionService != null && !permissionService.IsGranted)
                            {
                                logger.Information("OnStateChanged: NotificationEnabled is true in state but permission is not granted - syncing to OFF");
                                newValue = false;
                                // Update state to reflect actual permission status
                                DispatchScheduleUpdate(s => s.NotificationEnabled = false);
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.Error(ex, "OnStateChanged: Exception checking notification permission - assuming not granted");
                            // If permission check fails, assume not granted and sync to OFF
                            if (newValue)
                            {
                                newValue = false;
                                DispatchScheduleUpdate(s => s.NotificationEnabled = false);
                            }
                        }
#endif
                        
                        notificationEnabled = newValue;
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
                    _ = PopulateNumberOfTracksListViewAsync(newDefault);

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
            // Only handle permission check if this is a genuine user action (not from state sync or internal update)
            if (isUserAction && value)
            {
                logger.Debug("User toggled ON - checking notification permission");
                
                // Check current permission status
                bool isGranted = false;
                try
                {
                    if (permissionService != null)
                    {
                        isGranted = permissionService.IsGranted;
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "NotificationEnabled setter: Exception checking permission - assuming not granted");
                    isGranted = false;
                }
                
                if (isGranted)
                {
                    // Permission already granted - allow toggle ON
                    logger.Debug("Notification permission already granted - allowing toggle ON");
                    if (SetProperty(ref notificationEnabled, true))
                    {
                        DispatchScheduleUpdate(s => s.NotificationEnabled = true);
                    }
                }
                else
                {
                    // Permission not granted - toggle OFF immediately and request permission
                    logger.Debug("Notification permission not granted - setting toggle to OFF and requesting permission");
                    isWaitingForPermissionResponse = true;
                    
                    // Always set toggle to OFF immediately when permission is not granted
                    // Force update even if value is already false to ensure UI reflects the state
                    notificationEnabled = false;
                    OnPropertyChanged(nameof(NotificationEnabled));
                    DispatchScheduleUpdate(s => s.NotificationEnabled = false);
                    logger.Debug("Set NotificationEnabled to false - permission not granted");
                    
                    // Request permission - will fire PermissionGranted or PermissionDenied event
                    var requestInitiated = permissionService?.RequestPermissionIfNeeded() ?? false;
                    
                    if (!requestInitiated)
                    {
                        // Permission request was initiated - wait for event
                        // Toggle stays OFF until PermissionGranted event fires
                        logger.Debug("Permission request initiated - waiting for user response");
                    }
                    else
                    {
                        // Permission already granted (shouldn't happen due to check above, but handle it)
                        logger.Debug("Permission already granted after check - allowing toggle ON");
                        isWaitingForPermissionResponse = false;
                        notificationEnabled = true;
                        OnPropertyChanged(nameof(NotificationEnabled));
                        DispatchScheduleUpdate(s => s.NotificationEnabled = true);
                    }
                }
                return;
            }
            
            // For all other cases (toggling OFF, or internal updates), update immediately
            if (SetProperty(ref notificationEnabled, value))
            {
                DispatchScheduleUpdate(s => s.NotificationEnabled = value);
            }

            // Reset waiting flag if user toggles OFF
            if (isUserAction && !value)
            {
                logger.Debug("User toggled OFF - resetting permission wait flag");
                isWaitingForPermissionResponse = false;
            }
#elif IOS
            // Only handle permission check if this is a genuine user action (not from state sync or internal update)
            if (isUserAction && value)
            {
                logger.Debug("User toggled ON - checking iOS notification permission");
                
                // Check current permission status
                bool isGranted = false;
                try
                {
                    if (permissionService != null)
                    {
                        isGranted = permissionService.IsGranted;
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "NotificationEnabled setter (iOS): Exception checking permission - assuming not granted");
                    isGranted = false;
                }
                
                if (isGranted)
                {
                    // Permission already granted - allow toggle ON
                    logger.Debug("iOS notification permission already granted - allowing toggle ON");
                    if (SetProperty(ref notificationEnabled, true))
                    {
                        DispatchScheduleUpdate(s => s.NotificationEnabled = true);
                    }
                }
                else
                {
                    // Permission not granted - toggle OFF immediately and request permission
                    logger.Debug("iOS notification permission not granted - setting toggle to OFF and requesting permission");
                    isWaitingForPermissionResponse = true;
                    
                    // Always set toggle to OFF immediately when permission is not granted
                    // Force update even if value is already false to ensure UI reflects the state
                    notificationEnabled = false;
                    OnPropertyChanged(nameof(NotificationEnabled));
                    DispatchScheduleUpdate(s => s.NotificationEnabled = false);
                    logger.Debug("Set NotificationEnabled to false - permission not granted");
                    
                    // Request permission - will fire PermissionGranted or PermissionDenied event
                    var requestInitiated = permissionService?.RequestPermissionIfNeeded() ?? false;
                    
                    if (!requestInitiated)
                    {
                        // Permission request was initiated - wait for event
                        // Toggle stays OFF until PermissionGranted event fires
                        logger.Debug("Permission request initiated - waiting for user response");
                    }
                    else
                    {
                        // Permission already granted (shouldn't happen due to check above, but handle it)
                        logger.Debug("Permission already granted after check - allowing toggle ON");
                        isWaitingForPermissionResponse = false;
                        notificationEnabled = true;
                        OnPropertyChanged(nameof(NotificationEnabled));
                        DispatchScheduleUpdate(s => s.NotificationEnabled = true);
                    }
                }
                return;
            }
            
            // For all other cases (toggling OFF, or internal updates), update immediately
            if (SetProperty(ref notificationEnabled, value))
            {
                DispatchScheduleUpdate(s => s.NotificationEnabled = value);
            }

            // Reset waiting flag if user toggles OFF
            if (isUserAction && !value)
            {
                logger.Debug("User toggled OFF - resetting permission wait flag");
                isWaitingForPermissionResponse = false;
            }
#else
            // Non-Android/iOS platforms - update immediately
            if (SetProperty(ref notificationEnabled, value))
            {
                DispatchScheduleUpdate(s => s.NotificationEnabled = value);
            }
#endif
        }
    }

#if ANDROID || IOS
    public void StopPermissionCheckTaskIfRunning()
    {
        // Reset waiting flag if task was running
        isWaitingForPermissionResponse = false;
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
            _ = PopulateNumberOfTracksListViewAsync(selected);
            DispatchScheduleUpdate(s => s.NumberOfTracksToPlay = selected);
        }
    }

    /// <summary>
    /// True when the finite number-of-tracks row should be shown.
    /// </summary>
    public bool IsNumberOfTracksSelectionVisible => !PlayIndefinitely;

    /// <summary>
    /// Populates the number of tracks list view.
    /// This method is async because it may need to fetch publication data for dramas.
    /// </summary>
    public async Task PopulateNumberOfTracksListViewAsync(int? forceSelection = null)
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

    /// <summary>
    /// Private wrapper for backward compatibility with fire-and-forget calls.
    /// </summary>
    private async void PopulateNumberOfTracksListView(int? forceSelection = null)
    {
        await PopulateNumberOfTracksListViewAsync(forceSelection);
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
        if (permissionService != null)
        {
            permissionService.PermissionGranted -= OnPermissionGranted;
            permissionService.PermissionDenied -= OnPermissionDenied;
        }
#endif
    }
}

