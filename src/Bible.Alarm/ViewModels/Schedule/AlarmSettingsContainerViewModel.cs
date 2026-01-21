#nullable enable

using System.Windows.Input;
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
using Bible.Alarm.Services.UI.Interfaces;
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
                if (notificationEnabled != currentSchedule.NotificationEnabled)
                {
                    notificationEnabled = currentSchedule.NotificationEnabled;
                    OnPropertyChanged(nameof(NotificationEnabled));
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
            if (SetProperty(ref notificationEnabled, value))
            {
                // If enabling notifications, check/request permission first (Android 13+)
                if (value)
                {
#if ANDROID
                    logger.Information("NotificationEnabled toggled ON - checking/requesting permission");
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var granted = await NotificationPermissionHelper.RequestNotificationPermissionIfNeededAsync();
                            logger.Information("Notification permission check result: Granted={Granted}", granted);
                            if (!granted)
                            {
                                logger.Warning("Notification permission denied - reverting toggle");
                                MainThread.BeginInvokeOnMainThread(() =>
                                {
                                    notificationEnabled = false;
                                    OnPropertyChanged(nameof(NotificationEnabled));
                                });

                                var toastService = serviceProvider.GetService<IToastService>();
                                if (toastService != null)
                                {
                                    await toastService.ShowMessage(
                                        "Notification permission is required for tap-to-play alarms. Please enable notifications in system settings.",
                                        7);
                                }
                            }
                            else
                            {
                                logger.Information("Notification permission granted - updating schedule state");
                                // Permission granted - now dispatch the update to persist the change
                                MainThread.BeginInvokeOnMainThread(() =>
                                {
                                    DispatchScheduleUpdate(s => s.NotificationEnabled = true);
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.Error(ex, "Error requesting notification permission");
                            // On error, revert the toggle
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                notificationEnabled = false;
                                OnPropertyChanged(nameof(NotificationEnabled));
                            });
                        }
                    });
#else
                    // Non-Android platforms - dispatch immediately
                    DispatchScheduleUpdate(s => s.NotificationEnabled = value);
#endif
                }
                else
                {
                    // Disabling - no permission needed, dispatch immediately
                    DispatchScheduleUpdate(s => s.NotificationEnabled = value);
                }
            }
        }
    }

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
            BiblePublicationSectionNumber = source.BiblePublicationSectionNumber,
            BiblePublicationTrackNumber = source.BiblePublicationTrackNumber,
            BiblePublicationFinishedDuration = source.BiblePublicationFinishedDuration,
            MusicId = source.MusicId,
            MusicSectionCode = source.MusicSectionCode,
            MusicType = source.MusicType,
            MusicPublicationCode = source.MusicPublicationCode,
            MusicLanguageCode = source.MusicLanguageCode,
            MusicTrackNumber = source.MusicTrackNumber,
            MusicRepeat = source.MusicRepeat,
            BiblePublicationLanguageName = source.BiblePublicationLanguageName,
            BiblePublicationName = source.BiblePublicationName,
            BiblePublicationSectionName = source.BiblePublicationSectionName,
            MusicLanguageName = source.MusicLanguageName,
            MusicPublicationName = source.MusicPublicationName,
            MusicTrackName = source.MusicTrackName
        };
    }

    public void Dispose()
    {
        state.StateChanged -= OnStateChanged;
    }
}
