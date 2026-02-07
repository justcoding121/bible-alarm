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
    private NotificationPermissionPollingService? permissionPollingService;
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
        InitializeFromState();
    }

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
                if (permissionPollingService == null && notificationEnabled != currentSchedule.NotificationEnabled)
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
        // Stop any existing task
        StopPermissionCheckTask();

        // Create and configure the polling service
        permissionPollingService = new NotificationPermissionPollingService
        {
            GetCurrentValue = () => notificationEnabled,
            SetValue = value =>
            {
                isUpdatingFromPermissionCheck = true;
                try
                {
                    if (SetProperty(ref notificationEnabled, value))
                    {
                        DispatchScheduleUpdate(s => s.NotificationEnabled = value);
                        logger.Debug("Updated NotificationEnabled to {Value} and dispatched state update", value);
                    }
                }
                finally
                {
                    isUpdatingFromPermissionCheck = false;
                }
            },
            GetToastService = () => serviceProvider.GetService<IToastService>(),
            OnPermissionGranted = currentValue =>
            {
                logger.Information("Notification permission granted - updating toggle to ON. Current value: {Current}", currentValue);
            },
            OnPermissionDenied = currentValue =>
            {
                logger.Information("Notification permission denied - updating toggle to OFF. Current value: {Current}", currentValue);
            }
        };

        permissionPollingService.Start();
    }

    private void StopPermissionCheckTask()
    {
        if (permissionPollingService != null)
        {
            permissionPollingService.Stop();
            permissionPollingService.Dispose();
            permissionPollingService = null;
            logger.Debug("Stopped permission check task");
        }
    }

    public void StopPermissionCheckTaskIfRunning()
    {
        StopPermissionCheckTask();
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
        permissionPollingService?.Dispose();
#endif
    }
}
