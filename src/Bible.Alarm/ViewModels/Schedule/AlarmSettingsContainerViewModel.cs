#nullable enable

using System.Windows.Input;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;
#if ANDROID
using Bible.Alarm.Platforms.Android.Services.Helpers;
#endif

namespace Bible.Alarm.ViewModels.Schedule;

public sealed class AlarmSettingsContainerViewModel : ObservableObject, IDisposable
{
    private readonly ILogger logger;
    private readonly IServiceProvider serviceProvider;
    private readonly IState<ApplicationState> state;
    private readonly IDispatcher dispatcher;

    private int scheduleId;
    private bool isEnabled;
    private bool notificationEnabled;
    private bool isProcessingStateChange;
    private bool hasSignaledReady;
    private bool isReadyActionQueued;
#if ANDROID
    private bool isUpdatingFromPermissionCheck;
    private bool isSyncingFromState;
    private bool isWaitingForPermissionResponse;
    private NotificationPermissionService? permissionService;
#else
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
    private bool isUpdatingFromPermissionCheck;
    private bool isSyncingFromState;
    private bool isWaitingForPermissionResponse;
    private object? permissionService;
#pragma warning restore CS0649
#endif

    public AlarmSettingsContainerViewModel(
        ILogger logger,
        IServiceProvider serviceProvider,
        IState<ApplicationState> state,
        IDispatcher dispatcher)
    {
        this.logger = logger;
        this.serviceProvider = serviceProvider;
        this.state = state;
        this.dispatcher = dispatcher;

        state.StateChanged += OnStateChanged;
        InitializeCommands();
#if ANDROID
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
                
                // Show toast message to inform user (only shown when permission is denied)
                var toastService = serviceProvider.GetService<IToastService>();
                if (toastService != null)
                {
                    _ = toastService.ShowMessage("Notification permission is denied by Android", 5);
                }
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
        ToggleEnabledCommand = new RelayCommand(() => IsEnabled = !IsEnabled);
        ToggleNotificationEnabledCommand = new RelayCommand(() => NotificationEnabled = !NotificationEnabled);
    }

    private void InitializeFromState()
    {
        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule != null)
        {
            scheduleId = currentSchedule.Id;
            isEnabled = currentSchedule.IsEnabled;
            notificationEnabled = currentSchedule.NotificationEnabled;

#if ANDROID
            // If NotificationEnabled is true in state but permission is not granted, sync it to OFF silently
            // This handles the case where user revoked permission via Android settings
            // Don't request permission here - just sync the local property to match actual permission status
            // State will be synced in OnStateChanged to avoid interfering with container readiness signaling
            if (notificationEnabled && permissionService != null && !permissionService.IsGranted)
            {
                logger.Information("InitializeFromState: NotificationEnabled is true in state but permission is not granted - setting local property to OFF");
                notificationEnabled = false;
            }
#endif

            OnPropertyChanged(nameof(IsEnabled));
            OnPropertyChanged(nameof(NotificationEnabled));

            SignalContainerReady();
        }
    }

    private void SignalContainerReady()
    {
        if (hasSignaledReady || state.Value.ContainerReadiness.AlarmSettings) return;
        if (isReadyActionQueued) return;

        isReadyActionQueued = true;
        hasSignaledReady = true;

        if (state.Value.ContainerReadiness.AlarmSettings)
        {
            isReadyActionQueued = false;
            hasSignaledReady = true;
            return;
        }

        MainThread.BeginInvokeOnMainThread(() =>
        {
            isReadyActionQueued = false;

            if (state.Value.ContainerReadiness.AlarmSettings)
            {
                hasSignaledReady = true;
                return;
            }
            dispatcher.Dispatch(new ContainerReadyAction("AlarmSettings"));
        });
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (isProcessingStateChange)
        {
            return;
        }

        isProcessingStateChange = true;
        try
        {
            var stateValue = state.Value;
            var currentSchedule = stateValue.CurrentSchedule;

            if (hasSignaledReady && !stateValue.ContainerReadiness.AlarmSettings && currentSchedule != null)
            {
                hasSignaledReady = false;
                isReadyActionQueued = false;
                InitializeFromState();
                return;
            }

            if (scheduleId == 0 && currentSchedule != null && !hasSignaledReady)
            {
                InitializeFromState();
                return;
            }

            if (currentSchedule != null && currentSchedule.Id != scheduleId && currentSchedule.Id > 0)
            {
                hasSignaledReady = false;
                isReadyActionQueued = false;
                InitializeFromState();
            }
            else if (currentSchedule != null)
            {
                if (isEnabled != currentSchedule.IsEnabled)
                {
                    isEnabled = currentSchedule.IsEnabled;
                    OnPropertyChanged(nameof(IsEnabled));
                }
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
                        if (newValue && permissionService != null && !permissionService.IsGranted)
                        {
                            logger.Information("OnStateChanged: NotificationEnabled is true in state but permission is not granted - syncing to OFF");
                            newValue = false;
                            // Update state to reflect actual permission status
                            DispatchScheduleUpdate(s => s.NotificationEnabled = false);
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
            }
        }
        finally
        {
            isProcessingStateChange = false;
        }
    }

    public ICommand ToggleEnabledCommand { get; private set; } = null!;
    public ICommand ToggleNotificationEnabledCommand { get; private set; } = null!;

    public bool IsEnabled
    {
        get => isEnabled;
        set
        {
            if (SetProperty(ref isEnabled, value))
            {
                DispatchScheduleUpdate(s => s.IsEnabled = value);
            }
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
                if (permissionService != null && permissionService.IsGranted)
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
    public void StopPermissionCheckTaskIfRunning()
    {
        // Reset waiting flag if task was running
        isWaitingForPermissionResponse = false;
    }
#endif

    private void DispatchScheduleUpdate(Action<ScheduleStateItem> updateAction)
    {
        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule == null)
        {
            return;
        }

        var updatedSchedule = CloneScheduleStateItem(currentSchedule);
        updateAction(updatedSchedule);
        dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(updatedSchedule, false, false, shouldSave: false));
    }

    private static ScheduleStateItem CloneScheduleStateItem(ScheduleStateItem source)
    {
        return new ScheduleStateItem
        {
            Id = source.Id,
            Name = source.Name,
            IsEnabled = source.IsEnabled,
            Hour = source.Hour,
            Minute = source.Minute,
            Second = source.Second,
            DaysOfWeek = source.DaysOfWeek,
            NotificationEnabled = source.NotificationEnabled,
            MusicEnabled = source.MusicEnabled,
            SnoozeMinutes = source.SnoozeMinutes,
            NumberOfTracksToPlay = source.NumberOfTracksToPlay,
            AlwaysPlayFromStart = source.AlwaysPlayFromStart,
            CurrentPlayItem = source.CurrentPlayItem,
            LatestAlarmNotificationId = source.LatestAlarmNotificationId,
            BiblePublicationScheduleId = source.BiblePublicationScheduleId,
            BiblePublicationLanguageCode = source.BiblePublicationLanguageCode,
            BiblePublicationCode = source.BiblePublicationCode,
            BiblePublicationSectionCode = source.BiblePublicationSectionCode,
            BiblePublicationTrackCode = source.BiblePublicationTrackCode,
            BiblePublicationFinishedDuration = source.BiblePublicationFinishedDuration,
            MusicId = source.MusicId,
            MusicSectionCode = source.MusicSectionCode,
            MusicPublicationCode = source.MusicPublicationCode,
            MusicLanguageCode = source.MusicLanguageCode,
            MusicTrackCode = source.MusicTrackCode,
            MusicRepeat = source.MusicRepeat,
            BiblePublicationCategoryId = source.BiblePublicationCategoryId,
            BiblePublicationCategoryName = source.BiblePublicationCategoryName,
            BiblePublicationLanguageName = source.BiblePublicationLanguageName,
            BiblePublicationLanguageDirection = source.BiblePublicationLanguageDirection,
            BiblePublicationName = source.BiblePublicationName,
            BiblePublicationSectionName = source.BiblePublicationSectionName,
            BiblePublicationTrackTitle = source.BiblePublicationTrackTitle,
            MusicLanguageName = source.MusicLanguageName,
            MusicLanguageDirection = source.MusicLanguageDirection,
            MusicPublicationName = source.MusicPublicationName,
            MusicSectionName = source.MusicSectionName,
            MusicTrackName = source.MusicTrackName,
            BiblePublicationModalItemCount = source.BiblePublicationModalItemCount,
            BiblePublicationSectionModalItemCount = source.BiblePublicationSectionModalItemCount,
            MusicPublicationModalItemCount = source.MusicPublicationModalItemCount,
            MusicSectionModalItemCount = source.MusicSectionModalItemCount
        };
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
