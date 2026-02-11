#nullable enable

using System.Windows.Input;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
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
    private readonly ContainerReadySignaler containerReadySignaler;
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
        containerReadySignaler = new ContainerReadySignaler(state, dispatcher, "AlarmSettings", s => s.ContainerReadiness.AlarmSettings);

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
                
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await NotificationPermissionDeniedModalHelper.ShowAsync(
                        logger, navigationService, serviceProvider,
                        onPermissionGranted: () =>
                        {
                            isUpdatingFromPermissionCheck = true;
                            try
                            {
#if ANDROID
                                notificationEnabled = true;
                                OnPropertyChanged(nameof(NotificationEnabled));
                                DispatchScheduleUpdate(s => s.NotificationEnabled = true);
                                logger.Information("Set NotificationEnabled to true after permission granted from modal");
#elif IOS
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

#if ANDROID || IOS
                notificationEnabled = NotificationPermissionSyncHelper.SyncValueWithPermission(
                    notificationEnabled,
                    () => permissionService != null && permissionService.IsGranted,
                    logger,
                    "InitializeFromState: NotificationEnabled is true in state but permission is not granted - setting local property to OFF",
                    "InitializeFromState: Exception checking notification permission",
                    () => { });
#if IOS
                isEnabled = NotificationPermissionSyncHelper.SyncValueWithPermission(
                    isEnabled,
                    () => permissionService != null && permissionService.IsGranted,
                    logger,
                    "InitializeFromState: IsEnabled is true in state but permission is not granted - setting local property to OFF",
                    "InitializeFromState: Exception checking notification permission (IsEnabled)",
                    () => { });
#endif
#endif

                OnPropertyChanged(nameof(IsEnabled));
                OnPropertyChanged(nameof(NotificationEnabled));

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
        if (isProcessingStateChange)
        {
            return;
        }

        isProcessingStateChange = true;
        try
        {
            var stateValue = state.Value;
            var currentSchedule = stateValue.CurrentSchedule;

            if (containerReadySignaler.HasSignaledReady && !stateValue.ContainerReadiness.AlarmSettings && currentSchedule != null)
            {
                containerReadySignaler.Reset();
                InitializeFromState();
                return;
            }

            if (scheduleId == 0 && currentSchedule != null && !containerReadySignaler.HasSignaledReady)
            {
                InitializeFromState();
                return;
            }

            if (currentSchedule != null && currentSchedule.Id != scheduleId && currentSchedule.Id > 0)
            {
                containerReadySignaler.Reset();
                InitializeFromState();
            }
            else if (currentSchedule != null)
            {
                // Sync IsEnabled from state
                if (isEnabled != currentSchedule.IsEnabled)
                {
                    var newIsEnabledValue = currentSchedule.IsEnabled;
                    
#if IOS
                    newIsEnabledValue = NotificationPermissionSyncHelper.SyncValueWithPermission(
                        newIsEnabledValue,
                        () => permissionService != null && permissionService.IsGranted,
                        logger,
                        "OnStateChanged: IsEnabled is true in state but permission is not granted - syncing to OFF",
                        "OnStateChanged: Exception checking permission for IsEnabled",
                        () => DispatchScheduleUpdate(s => s.IsEnabled = false));
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
#if ANDROID || IOS
                        newValue = NotificationPermissionSyncHelper.SyncValueWithPermission(
                            newValue,
                            () => permissionService != null && permissionService.IsGranted,
                            logger,
                            "OnStateChanged: NotificationEnabled is true in state but permission is not granted - syncing to OFF",
                            "OnStateChanged: Exception checking notification permission for NotificationEnabled",
                            () => DispatchScheduleUpdate(s => s.NotificationEnabled = false));
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
                // iOS: Check notification permission when enabling reminder
                // iOS always uses notifications for alarms, so permission is required for the reminder itself
                // There is no separate "Tap to Play" toggle on iOS
                // Invalidate cache and use async check to get fresh status (user may have disabled permission)
                bool isGranted = false;
                try
                {
                    if (permissionService != null)
                    {
                        // Invalidate cache to force fresh check
                        permissionService.InvalidateCache();
                        // Use async check to get real-time status
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                var result = await permissionService.IsGrantedAsync();
                                MainThread.BeginInvokeOnMainThread(() =>
                                {
                                    // If permission was just granted and toggle is off, enable it
                                    if (result && !isEnabled)
                                    {
                                        isUpdatingFromPermissionCheck = true;
                                        try
                                        {
                                            isEnabled = true;
                                            OnPropertyChanged(nameof(IsEnabled));
                                            DispatchScheduleUpdate(s => s.IsEnabled = true);
                                            logger.Information("IsEnabled setter (iOS): Permission granted - enabling reminder");
                                        }
                                        finally
                                        {
                                            isUpdatingFromPermissionCheck = false;
                                        }
                                    }
                                });
                            }
                            catch (Exception asyncEx)
                            {
                                logger.Error(asyncEx, "IsEnabled setter (iOS): Exception in async permission check");
                            }
                        });
                        
                        // Use synchronous check for immediate decision (may use cache, but we invalidated it)
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
                    // Permission not granted - toggle OFF immediately and request permission
                    logger.Debug("Cannot enable reminder on iOS - notification permission not granted");
                    isWaitingForPermissionResponse = true;
                    
                    // Always set toggle to OFF immediately when permission is not granted
                    // Force update even if value is already false to ensure UI reflects the state
                    isEnabled = false;
                    OnPropertyChanged(nameof(IsEnabled));
                    DispatchScheduleUpdate(s => s.IsEnabled = false);
                    logger.Debug("Set IsEnabled to false - permission not granted");
                    
                    permissionService.RequestPermissionIfNeeded();
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        await NotificationPermissionDeniedModalHelper.ShowAsync(
                            logger, navigationService, serviceProvider,
                            onPermissionGranted: () =>
                            {
                                isUpdatingFromPermissionCheck = true;
                                try
                                {
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
            
#if ANDROID || IOS
            if (isUserAction && value &&
                NotificationEnabledToggleHandler.TryHandleToggleOnWhenNotGranted(
                    value,
                    () => permissionService != null && permissionService.IsGranted,
                    () => permissionService?.RequestPermissionIfNeeded() ?? false,
                    () =>
                    {
                        notificationEnabled = false;
                        OnPropertyChanged(nameof(NotificationEnabled));
                        DispatchScheduleUpdate(s => s.NotificationEnabled = false);
                    },
                    () =>
                    {
                        notificationEnabled = true;
                        OnPropertyChanged(nameof(NotificationEnabled));
                        DispatchScheduleUpdate(s => s.NotificationEnabled = true);
                    },
                    x => isWaitingForPermissionResponse = x,
                    logger,
                    "NotificationEnabled setter"))
            {
                return;
            }

            if (SetProperty(ref notificationEnabled, value))
            {
                DispatchScheduleUpdate(s => s.NotificationEnabled = value);
            }

            if (isUserAction && !value)
            {
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

        var updatedSchedule = ScheduleStateHelper.CloneScheduleStateItem(currentSchedule);
        updateAction(updatedSchedule);
        dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(updatedSchedule, false, false, shouldSave: false));
    }

    public void Dispose()
    {
        state.StateChanged -= OnStateChanged;
#if ANDROID || IOS
        if (permissionService != null)
        {
            permissionService.PermissionGranted -= OnPermissionGranted;
            permissionService.PermissionDenied -= OnPermissionDenied;
        }
#endif
    }
}
