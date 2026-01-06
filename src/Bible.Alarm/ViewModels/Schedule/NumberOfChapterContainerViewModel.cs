#nullable enable

using System.Collections.ObjectModel;
using System.Windows.Input;
using Bible.Alarm.Common.Interfaces.Battery;
using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Battery.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.Shared;
using Bible.Alarm.ViewModels.ScheduleViewModelHelpers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Essentials;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.Schedule;

public sealed class NumberOfChapterContainerViewModel : ObservableObject, IDisposable
{
    private readonly ILogger logger;
    private readonly INavigationService navigationService;
    private readonly IServiceProvider serviceProvider;
    private readonly IState<ApplicationState> state;
    private readonly IDispatcher dispatcher;

    private int scheduleId;
    private bool notificationEnabled;
    private bool alwaysPlayFromStart;
    private bool hasSignaledReady;
    private bool isReadyActionQueued;

    private ObservableCollection<NumberOfChaptersListViewItemModel> numberOfChaptersList = new();
    private NumberOfChaptersListViewItemModel? currentNumberOfChapters;

    public NumberOfChapterContainerViewModel(
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
        InitializeFromState();
    }

    private void InitializeCommands()
    {
        OpenModalCommand = new AsyncRelayCommand(async () =>
        {
            await navigationService.OpenNumberOfChaptersModalAsync(this);
        });

        SelectNumberOfChaptersCommand = new AsyncRelayCommand<NumberOfChaptersListViewItemModel>(async x =>
        {
            if (CurrentNumberOfChapters != null)
            {
                CurrentNumberOfChapters.IsSelected = false;
            }

            CurrentNumberOfChapters = x;
            if (CurrentNumberOfChapters != null)
            {
                CurrentNumberOfChapters.IsSelected = true;
            }

            // Dispatch update to state
            if (CurrentNumberOfChapters != null)
            {
                DispatchScheduleUpdate(s => s.NumberOfChaptersToRead = CurrentNumberOfChapters.Value);
            }

            // Explicitly notify property changes to ensure UI binding updates
            OnPropertyChanged(nameof(CurrentNumberOfChapters));
            OnPropertyChanged(nameof(CurrentNumberOfChaptersText));

            await navigationService.PopModalAsync();
        });

        ToggleAlwaysPlayFromStartCommand = new RelayCommand(() => AlwaysPlayFromStart = !AlwaysPlayFromStart);

        NotificationEnabledCommand = new RelayCommand(() => { NotificationEnabled = !NotificationEnabled; });

        CloseModalCommand = new AsyncRelayCommand(async () =>
        {
            await navigationService.PopModalAsync();
        });

        BatteryOptimizationExcludeCommand = new AsyncRelayCommand(async () =>
        {
            if (DeviceInfo.Platform == DevicePlatform.Android)
            {
                var batteryService = serviceProvider.GetService<IBatteryOptimizationService>();
                if (batteryService != null)
                {
                    await MarkBatteryOptimizationModalAsShown();
                    await navigationService.PopModalAsync();
                    batteryService.ShowOptimizationSettingsPage();
                }
            }
        });

        BatteryOptimizationDismissCommand = new AsyncRelayCommand(async () =>
        {
            await MarkBatteryOptimizationModalAsShown();
            await navigationService.PopModalAsync();
        });

        DoNotDisturbExcludeCommand = new AsyncRelayCommand(async () =>
        {
            if (DeviceInfo.Platform == DevicePlatform.Android)
            {
                var batteryService = serviceProvider.GetService<IBatteryOptimizationService>();
                if (batteryService != null)
                {
                    batteryService.ShowDoNotDisturbSettingsPage();
                }
            }
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

            PopulateNumberOfChaptersListView();

            OnPropertyChanged(nameof(NotificationEnabled));
            OnPropertyChanged(nameof(AlwaysPlayFromStart));
            
            // Signal that this container is ready (initialized from CurrentSchedule)
            SignalContainerReady();
        }
    }

    private void SignalContainerReady()
    {
        // Check if already signaled or already marked ready in state
        // This check must happen first to prevent any duplicate work
        if (hasSignaledReady || state.Value.ContainerReadiness.NumberOfChapter) return;
        
        // Check if action is already queued to prevent duplicate queued actions
        // This prevents multiple rapid calls from queuing multiple actions
        if (isReadyActionQueued) return;
        
        // Atomically set both flags to prevent race conditions
        // If another thread/call checks between these lines, it will see isReadyActionQueued=true
        isReadyActionQueued = true;
        hasSignaledReady = true;
        
        // Double-check state immediately after setting flags (before queuing)
        // This catches the case where state changed between the initial check and flag setting
        if (state.Value.ContainerReadiness.NumberOfChapter)
        {
            // State already shows ready, reset flags and return
            isReadyActionQueued = false;
            hasSignaledReady = true;
            return;
        }
        
        // Dispatch to state that this container is ready
        // Check state again inside the queued action to prevent duplicates from queued actions
        MainThread.BeginInvokeOnMainThread(() =>
        {
            isReadyActionQueued = false; // Reset flag when action executes
            
            // Final check before dispatching - if state already shows we're ready, another action already handled it
            if (state.Value.ContainerReadiness.NumberOfChapter)
            {
                // Ensure flag is set to prevent future attempts
                hasSignaledReady = true;
                return;
            }
            dispatcher.Dispatch(new ContainerReadyAction("NumberOfChapter"));
        });
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        var stateValue = state.Value;
        var currentSchedule = stateValue.CurrentSchedule;
        
        // If ContainerReadiness was reset to NotReady but we've already signaled ready, reset our flag
        // This handles the case where ViewScheduleAction resets ContainerReadiness after containers signaled ready
        if (hasSignaledReady && !stateValue.ContainerReadiness.NumberOfChapter && currentSchedule != null)
        {
            hasSignaledReady = false;
            isReadyActionQueued = false; // Reset queued flag as well
            // Re-initialize and signal ready again
            InitializeFromState();
            return;
        }
        
        // If we don't have a scheduleId yet (initial state), initialize when CurrentSchedule is set
        // But only if we haven't already signaled ready (prevents infinite loop for new schedules with Id=0)
        if (scheduleId == 0 && currentSchedule != null && !hasSignaledReady)
        {
            InitializeFromState();
            return;
        }
        
        // Initialize if schedule ID changed to a different positive ID (existing schedule opened)
        if (currentSchedule != null && currentSchedule.Id != scheduleId && currentSchedule.Id > 0)
        {
            hasSignaledReady = false; // Reset for new schedule
            isReadyActionQueued = false; // Reset queued flag as well
            InitializeFromState();
        }
        else if (currentSchedule != null)
        {
            // Update properties if schedule changed
            if (notificationEnabled != currentSchedule.NotificationEnabled)
            {
                notificationEnabled = currentSchedule.NotificationEnabled;
                OnPropertyChanged(nameof(NotificationEnabled));
            }
            if (alwaysPlayFromStart != currentSchedule.AlwaysPlayFromStart)
            {
                alwaysPlayFromStart = currentSchedule.AlwaysPlayFromStart;
                OnPropertyChanged(nameof(AlwaysPlayFromStart));
            }
        }
    }

    public ICommand OpenModalCommand { get; private set; } = null!;
    public ICommand SelectNumberOfChaptersCommand { get; private set; } = null!;
    public ICommand ToggleAlwaysPlayFromStartCommand { get; private set; } = null!;
    public ICommand NotificationEnabledCommand { get; private set; } = null!;
    public ICommand CloseModalCommand { get; private set; } = null!;

    public ObservableCollection<NumberOfChaptersListViewItemModel> NumberOfChaptersList
    {
        get => numberOfChaptersList;
        set => SetProperty(ref numberOfChaptersList, value);
    }

    public NumberOfChaptersListViewItemModel? CurrentNumberOfChapters
    {
        get => currentNumberOfChapters;
        set
        {
            if (SetProperty(ref currentNumberOfChapters, value))
            {
                // Notify that the Text property (computed from CurrentNumberOfChapters) has changed
                OnPropertyChanged(nameof(CurrentNumberOfChaptersText));
            }
        }
    }

    /// <summary>
    /// Computed property for binding to the number of chapters text in the UI.
    /// This ensures the UI updates when CurrentNumberOfChapters changes.
    /// </summary>
    public string CurrentNumberOfChaptersText => CurrentNumberOfChapters?.Text ?? string.Empty;

    public bool NotificationEnabled
    {
        get => notificationEnabled;
        set
        {
            if (SetProperty(ref notificationEnabled, value))
            {
                DispatchScheduleUpdate(s => s.NotificationEnabled = value);
            }
        }
    }

    private bool canOptimizeBattery;

    public bool CanOptimizeBattery
    {
        get => canOptimizeBattery;
        set => SetProperty(ref canOptimizeBattery, value);
    }

    public ICommand BatteryOptimizationExcludeCommand { get; private set; } = null!;
    public ICommand BatteryOptimizationDismissCommand { get; private set; } = null!;
    public ICommand DoNotDisturbExcludeCommand { get; private set; } = null!;

    private async Task MarkBatteryOptimizationModalAsShown()
    {
        if (DeviceInfo.Platform == DevicePlatform.Android)
        {
            var batteryService = serviceProvider.GetService<IBatteryOptimizationService>();
            if (batteryService != null)
            {
                await batteryService.MarkModalAsShownAsync();
            }
        }
    }

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

    private void PopulateNumberOfChaptersListView()
    {
        // Preserve the current selection if user has made one
        var preservedSelection = CurrentNumberOfChapters?.Value;
        var currentSchedule = state.Value.CurrentSchedule;
        var numberOfChapters = currentSchedule?.NumberOfChaptersToRead ?? 3;

        var chapterVMs = new ObservableCollection<NumberOfChaptersListViewItemModel>();

        for (var i = 1; i <= 21; i++)
        {
            var chaptersVm = new NumberOfChaptersListViewItemModel(i);

            // If user has made a selection, use that; otherwise use the state's value
            var shouldSelect = preservedSelection.HasValue
                ? preservedSelection.Value == i
                : numberOfChapters == i;

            if (shouldSelect)
            {
                chaptersVm.IsSelected = true;
                CurrentNumberOfChapters = chaptersVm;
            }

            chapterVMs.Add(chaptersVm);
        }

        NumberOfChaptersList = chapterVMs;
    }

    private void DispatchScheduleUpdate(Action<ScheduleStateItem> updateAction)
    {
        var currentSchedule = state.Value.CurrentSchedule;
        if (currentSchedule == null)
        {
            return;
        }

        // Clone the current schedule and apply the update
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
            NumberOfChaptersToRead = source.NumberOfChaptersToRead,
            AlwaysPlayFromStart = source.AlwaysPlayFromStart,
            CurrentPlayItem = source.CurrentPlayItem,
            LatestAlarmNotificationId = source.LatestAlarmNotificationId,
            BibleReadingScheduleId = source.BibleReadingScheduleId,
            BibleReadingLanguageCode = source.BibleReadingLanguageCode,
            BibleReadingPublicationCode = source.BibleReadingPublicationCode,
            BibleReadingBookNumber = source.BibleReadingBookNumber,
            BibleReadingChapterNumber = source.BibleReadingChapterNumber,
            BibleReadingFinishedDuration = source.BibleReadingFinishedDuration,
            MusicId = source.MusicId,
            MusicType = source.MusicType,
            MusicPublicationCode = source.MusicPublicationCode,
            MusicLanguageCode = source.MusicLanguageCode,
            MusicTrackNumber = source.MusicTrackNumber,
            MusicRepeat = source.MusicRepeat,
            BibleReadingLanguageName = source.BibleReadingLanguageName,
            BibleReadingPublicationName = source.BibleReadingPublicationName,
            BibleReadingBookName = source.BibleReadingBookName,
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

