#nullable enable

using System.Windows.Input;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.General;
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

    private readonly INavigationService navigationService;

    public AlarmSettingsContainerViewModel(
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
#if ANDROID
                // Android: Set NotificationEnabled (tap to play) to ON when permission is granted
                var oldValue = notificationEnabled;
                notificationEnabled = true;
                OnPropertyChanged(nameof(NotificationEnabled));
                DispatchScheduleUpdate(s => s.NotificationEnabled = true);
                logger.Information("Set NotificationEnabled to true after permission granted. Old value: {OldValue}, New value: {NewValue}", 
                    oldValue, notificationEnabled);
#elif IOS
                // iOS: Set IsEnabled (reminder) to ON when permission is granted
                // On iOS, there's no separate "tap to play" - the reminder itself requires permission
                if (!isEnabled)
                {
                    logger.Information("Permission granted on iOS - enabling reminder toggle");
                    isEnabled = true;
                    OnPropertyChanged(nameof(IsEnabled));
                    DispatchScheduleUpdate(s => s.IsEnabled = true);
                }
#endif
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
#if ANDROID
                // Android: Set NotificationEnabled (tap to play) to OFF when permission is denied
                notificationEnabled = false;
                OnPropertyChanged(nameof(NotificationEnabled));
                DispatchScheduleUpdate(s => s.NotificationEnabled = false);
                logger.Debug("Set NotificationEnabled to false after permission denied");
#elif IOS
                // iOS: Set IsEnabled (reminder) to OFF when permission is denied
                // On iOS, there's no separate "tap to play" - the reminder itself requires permission
                isEnabled = false;
                OnPropertyChanged(nameof(IsEnabled));
                DispatchScheduleUpdate(s => s.IsEnabled = false);
                logger.Debug("Set IsEnabled to false after permission denied on iOS");
#endif
                
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
#if ANDROID
                                            // Android: Set NotificationEnabled (tap to play) to ON when permission is granted
                                            notificationEnabled = true;
                                            OnPropertyChanged(nameof(NotificationEnabled));
                                            DispatchScheduleUpdate(s => s.NotificationEnabled = true);
                                            logger.Information("Set NotificationEnabled to true after permission granted from modal");
#elif IOS
                                            // iOS: Set IsEnabled (reminder) to ON when permission is granted
                                            isEnabled = true;
                                            OnPropertyChanged(nameof(IsEnabled));
                                            DispatchScheduleUpdate(s => s.IsEnabled = true);
                                            logger.Information("Set IsEnabled to true after permission granted from modal");
#endif
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
        ToggleEnabledCommand = new RelayCommand(() => IsEnabled = !IsEnabled);
        ToggleNotificationEnabledCommand = new RelayCommand(() => NotificationEnabled = !NotificationEnabled);
    }

    private void InitializeFromState()
    {
        try
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
                    
                    // If IsEnabled (reminder) is true in state but permission is not granted, sync it to OFF silently
                    // iOS always uses notifications for alarms, so permission is required for the reminder itself
                    // This handles the case where user revoked permission via iOS settings
                    // Don't request permission here - just sync the local property to match actual permission status
                    // State will be synced in OnStateChanged to avoid interfering with container readiness signaling
                    if (isEnabled && permissionService != null && !permissionService.IsGranted)
                    {
                        logger.Information("InitializeFromState: IsEnabled is true in state but permission is not granted - setting local property to OFF");
                        isEnabled = false;
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
                    if (isEnabled)
                    {
                        isEnabled = false;
                    }
                }
#endif

                OnPropertyChanged(nameof(IsEnabled));
                OnPropertyChanged(nameof(NotificationEnabled));

                SignalContainerReady();
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "InitializeFromState: Exception initializing from state");
            // Don't rethrow - allow app to continue even if initialization fails
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
                // Sync IsEnabled from state
                if (isEnabled != currentSchedule.IsEnabled)
                {
                    var newIsEnabledValue = currentSchedule.IsEnabled;
                    
#if IOS
                    // If state has IsEnabled=true but permission is not granted, sync to OFF
                    // iOS always uses notifications for alarms, so permission is required for the reminder itself
                    // This handles the case where user revoked permission via iOS settings
                    try
                    {
                        bool isGranted = false;
                        try
                        {
                            if (permissionService != null)
                            {
                                isGranted = permissionService.IsGranted;
                            }
                        }
                        catch (Exception permEx)
                        {
                            logger.Error(permEx, "OnStateChanged: Exception checking permission for IsEnabled - assuming not granted");
                            isGranted = false;
                        }
                        
                        if (newIsEnabledValue && !isGranted)
                        {
                            logger.Information("OnStateChanged: IsEnabled is true in state but permission is not granted - syncing to OFF");
                            newIsEnabledValue = false;
                            // Update state to reflect actual permission status
                            DispatchScheduleUpdate(s => s.IsEnabled = false);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, "OnStateChanged: Exception checking notification permission for IsEnabled - assuming not granted");
                        // If permission check fails, assume not granted and sync to OFF
                        if (newIsEnabledValue)
                        {
                            newIsEnabledValue = false;
                            DispatchScheduleUpdate(s => s.IsEnabled = false);
                        }
                    }
#endif
                    
                    isEnabled = newIsEnabledValue;
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
                            logger.Error(ex, "OnStateChanged: Exception checking notification permission for NotificationEnabled - assuming not granted");
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
                            logger.Error(ex, "OnStateChanged: Exception checking notification permission for NotificationEnabled - assuming not granted");
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
#if IOS
            // iOS: Check notification permission when enabling reminder
            // iOS always uses notifications for alarms, so permission is required for the reminder itself
            // There is no separate "Tap to Play" toggle on iOS
            if (value && !isUpdatingFromPermissionCheck && !isSyncingFromState)
            {
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
                    logger.Error(ex, "IsEnabled setter (iOS): Exception checking permission - assuming not granted");
                    isGranted = false;
                }
                
                if (!isGranted)
                {
                    logger.Debug("Cannot enable reminder on iOS - notification permission not granted");
                    isWaitingForPermissionResponse = true;
                    // Set toggle back to OFF immediately
                    isEnabled = false;
                    OnPropertyChanged(nameof(IsEnabled));
                    // Request permission - will fire PermissionGranted or PermissionDenied event
                    permissionService.RequestPermissionIfNeeded();
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
                                                // iOS: Set IsEnabled (reminder) to ON when permission is granted
                                                isEnabled = true;
                                                OnPropertyChanged(nameof(IsEnabled));
                                                DispatchScheduleUpdate(s => s.IsEnabled = true);
                                                logger.Information("Set IsEnabled to true after permission granted from modal");
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
                            logger.Error(ex, "Error opening notification permission modal when trying to enable reminder");
                        }
                    });
                    return;
                }
            }
#endif
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
