#nullable enable
using System;
using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.ScheduleListItemViewModelHelpers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels;

public sealed class ScheduleListItemViewModel(
    ILogger logger,
    ISchedulePlaybackService playbackService,
    IPlaybackService stopPlaybackService,
    IScheduleStateService scheduleStateService,
    IState<ApplicationState> applicationState,
    IState<PlaybackState> playbackState,
    IDispatcher dispatcher,
    IMapper mapper,
    ICategoryNameService categoryNameService)
    : ObservableObject, IComparable, IComparable<ScheduleListItemViewModel>, IEquatable<ScheduleListItemViewModel>, IDisposable
{
    // Helper classes
    private readonly ScheduleListItemInitializer initializer = new(logger, mapper, applicationState);
    private readonly ScheduleListItemPropertyManager propertyManager = new(logger, scheduleStateService);
    private readonly ScheduleListItemStateHandler stateHandler = new(logger, mapper, applicationState);
    private readonly ScheduleListItemSubtitleManager subtitleManager = new(logger, applicationState, playbackState);
    private readonly ScheduleListItemBibleDisplayNameProvider bibleDisplayNameProvider = new(applicationState, categoryNameService);
    private ScheduleListItemStateChangeApplier? stateChangeApplier;
    private ScheduleListItemStateChangeApplier StateChangeApplier => stateChangeApplier ??= new(logger, applicationState, stateHandler, propertyManager);

    private const int SpinnerTimeoutSeconds = 15;
    private bool isBusy;
    private bool isProcessingStateChange;
    private volatile bool isPlayCommandRunning;
    private volatile bool isShowModalPending;
    private CancellationTokenSource? spinnerTimeoutCts;
    public Action? OnPlayStarted { get; set; }

    /// <summary>
    /// Action to call when playback actually starts (modal is shown). Used to hide overlay on Home page.
    /// </summary>
    public Action? OnPlaybackStarted { get; set; }

    public AlarmSchedule? Schedule { get; private set; }

    /// <summary>
    /// Initializes the ScheduleListItem from pre-mapped AlarmSchedule data.
    /// This is optimized for performance - mapping happens off UI thread, only UI operations here.
    /// </summary>
    public void InitializeFromSchedule(AlarmSchedule schedule, ScheduleStateItem? scheduleStateItem = null)
    {
        var (validSchedule, stateItem) = initializer.InitializeFromSchedule(schedule, scheduleStateItem);
        if (validSchedule == null)
        {
            return;
        }

        InitializeCommon(validSchedule, stateItem);
    }

    /// <summary>
    /// Initializes the ScheduleListItem from state using the schedule ID.
    /// This follows the state-driven architecture pattern where view models initialize from state.
    /// </summary>
    public void SetScheduleId(int scheduleId)
    {
        var (schedule, scheduleStateItem) = initializer.SetScheduleId(scheduleId);
        if (schedule == null)
        {
            return;
        }

        InitializeCommon(schedule, scheduleStateItem);
    }

    /// <summary>
    /// Common initialization logic shared by InitializeFromSchedule and SetScheduleId.
    /// Sets up the schedule, property notifications, event subscriptions, and commands.
    /// </summary>
    private void InitializeCommon(AlarmSchedule schedule, ScheduleStateItem? scheduleStateItem)
    {
        ApplyScheduleFromPropertyManager(schedule);
        RaiseCoreSchedulePropertyNotifications();
        DisconnectScheduleSubscriptions();
        ConnectScheduleSubscriptions(scheduleStateItem);
        EnsureScheduleCommands();
        RefreshSubTitleFromState(scheduleStateItem);
    }

    private void ApplyScheduleFromPropertyManager(AlarmSchedule schedule)
    {
        propertyManager.IsInitializing = true;
        try
        {
            Schedule = schedule;
            var (isEnabled, _, _, _, _, _, _, _) = ScheduleListItemPropertyManager.GetPropertiesFromSchedule(schedule);
            propertyManager.IsEnabled = isEnabled;
        }
        finally
        {
            propertyManager.IsInitializing = false;
        }
    }

    private void RaiseCoreSchedulePropertyNotifications()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(CategoryName));
            OnPropertyChanged(nameof(CategoryNameDisplay));
            OnPropertyChanged(nameof(TimeText));
            OnPropertyChanged(nameof(Hour));
            OnPropertyChanged(nameof(Minute));
            OnPropertyChanged(nameof(Meridian));
            OnPropertyChanged(nameof(MeridianText));
            OnPropertyChanged(nameof(DaysOfWeek));
            OnPropertyChanged(nameof(IsEnabled));
            OnPropertyChanged(nameof(MusicEnabled));
            OnPropertyChanged(nameof(This));
        });
    }

    private void DisconnectScheduleSubscriptions()
    {
        applicationState.StateChanged -= OnApplicationStateChanged;
        playbackState.StateChanged -= OnPlaybackStateChanged;
        WeakReferenceMessenger.Default.Unregister<ThemeChangedMessage>(this);
        WeakReferenceMessenger.Default.Unregister<RequestShowPlaybackModalMessage>(this);
        WeakReferenceMessenger.Default.Unregister<PlaybackModalOpenedMessage>(this);
        WeakReferenceMessenger.Default.Unregister<PlaybackExplicitStopMessage>(this);
    }

    private void ConnectScheduleSubscriptions(ScheduleStateItem? scheduleStateItem)
    {
        applicationState.StateChanged += OnApplicationStateChanged;
        stateHandler.LastKnownSchedule = Schedule;

        if (scheduleStateItem != null)
        {
            stateHandler.LastKnownBiblePublicationLanguageName = scheduleStateItem.BiblePublicationLanguageName;
            stateHandler.LastKnownSectionName = scheduleStateItem.BiblePublicationSectionName;
            stateHandler.LastKnownTrackTitle = scheduleStateItem.BiblePublicationTrackTitle;
            stateHandler.LastKnownBiblePublicationCode = scheduleStateItem.BiblePublicationCode;
        }

        playbackState.StateChanged += OnPlaybackStateChanged;

        var initialPlayback = playbackState.Value;
        lastObservedGlobalPlaybackActive = initialPlayback.IsPreparingOrPlaying || initialPlayback.CurrentScheduleId.HasValue;

        SyncIsBusyWithPlaybackState();

        WeakReferenceMessenger.Default.Register<ThemeChangedMessage>(this, (r, m) => OnThemeChanged());
        WeakReferenceMessenger.Default.Register<RequestShowPlaybackModalMessage>(this, (r, m) => OnRequestShowPlaybackModal(m));
        WeakReferenceMessenger.Default.Register<PlaybackModalOpenedMessage>(this, (r, m) => SetIsBusy(false));
        WeakReferenceMessenger.Default.Register<PlaybackExplicitStopMessage>(this, (r, m) =>
        {
            isShowModalPending = false;
            isPlayCommandRunning = false;
            SyncIsBusyWithPlaybackState();
        });
    }

    private void EnsureScheduleCommands()
    {
        PlayCommand ??= new AsyncRelayCommand(async () =>
        {
            if (Schedule?.Id is not > 0 || isPlayCommandRunning)
            {
                return;
            }

            isPlayCommandRunning = true;
            IsBusy = true;
            StartSpinnerTimeout();
            try
            {
                await Task.Delay(50);
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    try
                    {
                        OnPlayStarted?.Invoke();
                        WeakReferenceMessenger.Default.Send(new RequestShowPlaybackModalMessage { TargetScheduleId = Schedule!.Id });
                        await playbackService.PlayScheduleAsync(Schedule!.Id);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        logger.Warning(ex, AppConstants.Logging.ScheduleListItemViewModelDiagnosticsLog.PlayCommandFailedForSchedule, Schedule!.Id);
                    }
                    finally
                    {
                        isPlayCommandRunning = false;
                        ClearShowModalPendingIfPlaybackNotStarted();
                        SyncIsBusyWithPlaybackState();
                    }
                });
            }
            catch (OperationCanceledException)
            {
                isPlayCommandRunning = false;
                ClearShowModalPendingIfPlaybackNotStarted();
                SyncIsBusyWithPlaybackState();
            }
            catch (Exception ex)
            {
                logger.Warning(ex, AppConstants.Logging.ScheduleListItemViewModelDiagnosticsLog.PlayCommandFailedBeforeSchedulingPlaybackForSchedule, Schedule?.Id ?? 0);
                isPlayCommandRunning = false;
                ClearShowModalPendingIfPlaybackNotStarted();
                SyncIsBusyWithPlaybackState();
            }
        }, AsyncRelayCommandOptions.AllowConcurrentExecutions);

        ToggleEnabledCommand ??= new RelayCommand(() =>
        {
            if (Schedule != null)
            {
                var currentValue = Schedule.IsEnabled;
                IsEnabled = !currentValue;
            }
        });

        DeleteCommand ??= new AsyncRelayCommand(async () =>
        {
            if (Schedule == null || Schedule.Id <= 0)
            {
                return;
            }

            var scheduleCount = applicationState.Value.Schedules?.Count ?? 0;
            if (scheduleCount <= 1)
            {
                WeakReferenceMessenger.Default.Send(new ShowToastMessage("Cannot delete last schedule"));
                return;
            }

            var currentPlayback = playbackState.Value;
            if (currentPlayback.IsPreparingOrPlaying && currentPlayback.CurrentScheduleId == Schedule.Id)
            {
                await stopPlaybackService.StopAsync();
            }

            dispatcher.Dispatch(new DeleteScheduleAction(Schedule.Id));
        });
    }

    public int ScheduleId => Schedule?.Id ?? 0;

    public string Name => DisplayTextHelper.NormalizeSingleLine(Schedule?.Name);

    public string CategoryName
        => bibleDisplayNameProvider.GetCategoryDisplayName(ScheduleId);

    /// <summary>
    /// Category name for display. Returns empty when category name equals schedule name to avoid duplicate text.
    /// </summary>
    public string CategoryNameDisplay
    {
        get
        {
            var category = CategoryName;
            var name = Name;
            if (string.IsNullOrWhiteSpace(category))
            {
                return string.Empty;
            }
            if (string.Equals(category.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }
            return category;
        }
    }

    public string SubTitle
    {
        get => subtitleManager.SubTitle;
        private set => subtitleManager.SubTitle = value;
    }

    public string Language
    {
        get => subtitleManager.Language;
        private set => subtitleManager.Language = value;
    }

    public string BiblePublicationName
        => bibleDisplayNameProvider.GetBiblePublicationName(ScheduleId);

    public string BiblePublicationSectionName
        => bibleDisplayNameProvider.GetBiblePublicationSectionName(ScheduleId);

    public string BiblePublicationTrackName
        => bibleDisplayNameProvider.GetBiblePublicationTrackName(ScheduleId);

    public bool IsBibleCategory
        => bibleDisplayNameProvider.IsBibleCategory(ScheduleId);

    /// <summary>
    /// Home page display: for Bible category, show "SectionName TrackCode" (e.g. "Exodus 9") as one line.
    /// </summary>
    public string BiblePublicationSectionAndTrackOneLine
        => bibleDisplayNameProvider.GetBiblePublicationSectionAndTrackOneLine(ScheduleId);

    public bool ShouldShowBiblePublicationSectionAndTrackOneLine
        => IsBibleCategory && !string.IsNullOrWhiteSpace(BiblePublicationSectionAndTrackOneLine);

    public bool ShouldShowBiblePublicationSectionNameLine
        => !IsBibleCategory && !string.IsNullOrWhiteSpace(BiblePublicationSectionName);

    public bool ShouldShowBiblePublicationTrackNameLine
        => !IsBibleCategory && !string.IsNullOrWhiteSpace(BiblePublicationTrackName);

    public bool MusicEnabled => Schedule?.MusicEnabled ?? false;

    /// <summary>
    /// True when the Language/Music row should be visible (Language not empty or MusicEnabled).
    /// Prevents the Grid from reserving space when both are empty.
    /// </summary>
    public bool ShouldShowLanguageOrMusicLine
        => !string.IsNullOrWhiteSpace(Language) || MusicEnabled;

    public bool IsEnabled
    {
        get => propertyManager.IsEnabled;
        set
        {
            if (propertyManager.IsEnabled != value)
            {
                if (value && Schedule?.DaysOfWeek == 0)
                {
                    WeakReferenceMessenger.Default.Send(new ShowToastMessage("Select at least one day"));
                    OnPropertyChanged();
                    return;
                }

                propertyManager.IsEnabled = value;
                OnPropertyChanged();
                if (!propertyManager.IsInitializing && Schedule != null)
                {
                    WeakReferenceMessenger.Default.Send(new ShowProgressBarMessage());
                    _ = HandleIsEnabledChanged(value);
                }
            }
        }
    }

    private async Task HandleIsEnabledChanged(bool newValue)
    {
        await propertyManager.HandleIsEnabledChanged(
            ScheduleId,
            newValue,
            Schedule,
            () => OnPropertyChanged(nameof(This)),
            () => NotifyPropertiesChanged(),
            async (attemptedValue) => await RevertIsEnabledChange(attemptedValue));
    }

    private async Task RevertIsEnabledChange(bool attemptedValue)
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            propertyManager.IsEnabled = !attemptedValue;
            OnPropertyChanged(nameof(IsEnabled));
            OnPropertyChanged(nameof(This));
            
            // Hide progress bar when operation fails and is reverted
            WeakReferenceMessenger.Default.Send(new HideProgressBarMessage());
        });
    }

    private void NotifyPropertiesChanged()
    {
        RaisePropertiesChangedEvent();
        OnPropertyChanged(nameof(This));
    }

    public WeekDays DaysOfWeek => Schedule?.DaysOfWeek ?? 0;

    public string TimeText => Schedule?.TimeText ?? string.Empty;

    public string Hour => Schedule?.MeridianHour.ToString("D2") ?? "00";

    public string Minute => Schedule?.Minute.ToString("D2") ?? "00";

    public Meridian Meridian => Schedule?.Meridian ?? Meridian.Am;

    public string MeridianText => Meridian.ToString().ToUpperInvariant();

    public ScheduleListItemViewModel This => this;

    public ICommand PlayCommand { get; private set; } = null!;

    public ICommand ToggleEnabledCommand { get; private set; } = null!;
    public ICommand DeleteCommand { get; private set; } = null!;

    public bool IsBusy
    {
        get => isBusy;
        set => SetProperty(ref isBusy, value);
    }

    private bool isNavigating;

    /// <summary>
    /// True while the schedule page is being pushed after the user taps this item.
    /// Drives a small spinner overlaid at the bottom-right of the list card.
    /// </summary>
    public bool IsNavigating
    {
        get => isNavigating;
        set => SetProperty(ref isNavigating, value);
    }

    public void RaisePropertiesChangedEvent()
    {
        var properties = GetType()
            .GetProperties()
            .Where(x => x.Name != "IsEnabled")
            .Select(x => x.Name);
        foreach (var property in properties)
        {
            OnPropertyChanged(property);
        }
    }

    /// <summary>
    /// Refreshes subtitle from ScheduleStateItem in state (uses pre-populated SectionName).
    /// State-only: Home list should not trigger ad-hoc DB calls.
    /// </summary>
    private void RefreshSubTitleFromState(ScheduleStateItem? providedScheduleStateItem = null)
    {
        if (Schedule == null || Schedule.Id <= 0)
        {
            return;
        }

        subtitleManager.RefreshSubTitleFromState(
            Schedule.Id,
            providedScheduleStateItem,
            value => SubTitle = value,
            value => Language = value,
            name =>
            {
                OnPropertyChanged(name);
                if (name == nameof(Language))
                {
                    OnPropertyChanged(nameof(ShouldShowLanguageOrMusicLine));
                }
            });
    }

    public void RefreshTrackName(bool force = false) =>
        // Refresh from state only (no DB fallback)
        RefreshSubTitleFromState();

    public int CompareTo(ScheduleListItemViewModel? other)
    {
        if (other is null)
        {
            return 1;
        }

        var thisPlayed = Schedule?.LastPlayedAtUtc ?? DateTime.MinValue;
        var otherPlayed = other.Schedule?.LastPlayedAtUtc ?? DateTime.MinValue;
        var playedCompare = otherPlayed.CompareTo(thisPlayed);
        if (playedCompare != 0)
        {
            return playedCompare;
        }

        return ScheduleId.CompareTo(other.ScheduleId);
    }

    public int CompareTo(object? obj) => CompareTo(obj as ScheduleListItemViewModel);

    public bool Equals(ScheduleListItemViewModel? other)
    {
        if (other is null)
        {
            return false;
        }

        var thisPlayed = Schedule?.LastPlayedAtUtc ?? DateTime.MinValue;
        var otherPlayed = other.Schedule?.LastPlayedAtUtc ?? DateTime.MinValue;
        return ScheduleId == other.ScheduleId && thisPlayed == otherPlayed;
    }

    public override bool Equals(object? obj) => Equals(obj as ScheduleListItemViewModel);

    public override int GetHashCode() => HashCode.Combine(ScheduleId, Schedule?.LastPlayedAtUtc ?? DateTime.MinValue);

    public static bool operator ==(ScheduleListItemViewModel? left, ScheduleListItemViewModel? right) =>
        ReferenceEquals(left, right) || left is not null && left.Equals(right);

    public static bool operator !=(ScheduleListItemViewModel? left, ScheduleListItemViewModel? right) => !(left == right);

    public static bool operator <(ScheduleListItemViewModel? left, ScheduleListItemViewModel? right) =>
        left is not null && right is not null && left.CompareTo(right) < 0;

    public static bool operator >(ScheduleListItemViewModel? left, ScheduleListItemViewModel? right) =>
        left is not null && right is not null && left.CompareTo(right) > 0;

    public static bool operator <=(ScheduleListItemViewModel? left, ScheduleListItemViewModel? right) =>
        left is not null && right is not null && left.CompareTo(right) <= 0;

    public static bool operator >=(ScheduleListItemViewModel? left, ScheduleListItemViewModel? right) =>
        left is not null && right is not null && left.CompareTo(right) >= 0;

    private void OnApplicationStateChanged(object? sender, EventArgs e)
    {
        var schedule = Schedule;
        if (schedule?.Id <= 0)
        {
            return;
        }

        if (schedule == null)
        {
            return;
        }

        // Prevent re-entrant calls to avoid cycles
        if (isProcessingStateChange)
        {
            return;
        }

        var changeInfo = stateHandler.HandleApplicationStateChanged(schedule.Id, schedule);
        if (changeInfo == null)
        {
            return;
        }

        // Check if there are any actual changes before processing
        var hasAnyChanges = changeInfo.AnyBibleSchedulePropertyChanged ||
                           changeInfo.DaysOfWeekChanged ||
                           changeInfo.IsEnabledChanged ||
                           changeInfo.NameChanged ||
                           changeInfo.TimeChanged ||
                           changeInfo.MusicEnabledChanged ||
                           changeInfo.TrackChanged;

        if (!hasAnyChanges)
        {
            return;
        }

        // Store old WeekDays before updating to ensure we can detect changes
        var oldDaysOfWeek = schedule.DaysOfWeek;

        isProcessingStateChange = true;
        try
        {
            StateChangeApplier.UpdateScheduleFromState(
                changeInfo,
                s => Schedule = s,
                updatedScheduleItem => RefreshSubTitleFromState(updatedScheduleItem),
                name => OnPropertyChanged(name),
                () => ScheduleId);
            StateChangeApplier.NotifyPropertyChanges(
                changeInfo,
                name => OnPropertyChanged(name),
                () => ScheduleId,
                () => Schedule,
                RaisePropertiesChangedEvent);
        }
        finally
        {
            isProcessingStateChange = false;
        }

        // Double-check WeekDays change after update (in case comparison missed it)
        // This handles edge cases where the schedule was already updated but WeekDays changed
        var updatedSchedule = changeInfo.UpdatedSchedule;
        if (!changeInfo.DaysOfWeekChanged && updatedSchedule != null && updatedSchedule.DaysOfWeek != oldDaysOfWeek)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                OnPropertyChanged(nameof(DaysOfWeek));
                // Also notify 'This' to trigger converters that bind to the entire ViewModel
                OnPropertyChanged(nameof(This));
            });
        }
    }

    private bool lastObservedGlobalPlaybackActive;

    private void OnPlaybackStateChanged(object? sender, EventArgs e)
    {
        ResetPlayCommandGateIfPlaybackSessionEnded();
        SyncIsBusyWithPlaybackState();
    }

    /// <summary>
    /// Sets IsBusy when a play-modal request targets this schedule row.
    /// Covers car listing, Android Auto, alarm triggers, and the home-list play button,
    /// giving immediate spinner feedback regardless of which surface initiated playback.
    /// If IsBusy is already true (e.g. PlayCommand set it first) this is a no-op.
    /// </summary>
    private void OnRequestShowPlaybackModal(RequestShowPlaybackModalMessage message)
    {
        if (message.TargetScheduleId != ScheduleId || ScheduleId <= 0)
        {
            return;
        }

        isShowModalPending = true;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (!isBusy)
            {
                IsBusy = true;
                StartSpinnerTimeout();
            }
        });
    }

    /// <summary>
    /// Clears a stuck <see cref="isPlayCommandRunning"/> when global playback transitions from active to idle.
    /// Each list row tracks the previous snapshot itself so every row sees the same edge regardless of
    /// Fluxor subscriber order, and we do not clear during a fresh tap before PlaybackState shows active.
    /// </summary>
    private void ResetPlayCommandGateIfPlaybackSessionEnded()
    {
        var state = playbackState.Value;
        var activeNow = state.IsPreparingOrPlaying || state.CurrentScheduleId.HasValue;
        var sessionJustEnded = lastObservedGlobalPlaybackActive && !activeNow;
        lastObservedGlobalPlaybackActive = activeNow;

        if (!sessionJustEnded || !isPlayCommandRunning || isBusy)
        {
            return;
        }

        void ApplyReset()
        {
            var snapshot = playbackState.Value;
            if (snapshot.IsPreparingOrPlaying || snapshot.CurrentScheduleId.HasValue || !isPlayCommandRunning || isBusy)
            {
                return;
            }

            logger.Debug(AppConstants.Logging.ScheduleListItemViewModelDiagnosticsLog.ClearingPlayCommandGatePlaybackSessionEnded, ScheduleId);
            isPlayCommandRunning = false;
        }

        if (MainThread.IsMainThread)
        {
            ApplyReset();
        }
        else
        {
            MainThread.BeginInvokeOnMainThread(ApplyReset);
        }
    }

    /// <summary>
    /// Error/end fallback for the play-button spinner.
    ///
    /// Normal path: PlaybackModalOpenedMessage (from PlaybackModal.Loaded) clears the spinner.
    /// This method only clears the spinner when the modal will never open — i.e. playback
    /// ended or failed before the modal could be pushed.
    ///
    /// Gap protection: while isPlayCommandRunning is true the old schedule may briefly stop
    /// before the new one starts loading. Never clear during this transient gap.
    /// </summary>
    private void SyncIsBusyWithPlaybackState()
    {
        if (ScheduleId <= 0 || !isBusy)
        {
            return;
        }

        // While PlayCommand or a show-modal request is in flight, never clear —
        // gap protection during schedule switches (home-page play and car/alarm/external play).
        if (isPlayCommandRunning || isShowModalPending)
        {
            return;
        }

        var state = playbackState.Value;

        // Our schedule is actively loading/playing — the modal will open and clear the spinner.
        if (state.CurrentScheduleId == ScheduleId && state.IsPreparingOrPlaying)
        {
            return;
        }

        // Playback ended or failed, or a different schedule took over. Modal won't open for us.
        SetIsBusy(false);
    }

    private void ClearShowModalPendingIfPlaybackNotStarted()
    {
        if (!isShowModalPending)
        {
            return;
        }

        var state = playbackState.Value;
        if (!state.IsPreparingOrPlaying || state.CurrentScheduleId != ScheduleId)
        {
            isShowModalPending = false;
        }
    }

    private void SetIsBusy(bool value)
    {
        if (value == IsBusy)
        {
            return;
        }

        if (!value)
        {
            CancelSpinnerTimeout();
            isShowModalPending = false;
        }

        if (MainThread.IsMainThread)
        {
            IsBusy = value;
        }
        else
        {
            MainThread.BeginInvokeOnMainThread(() => IsBusy = value);
        }
    }

    private void StartSpinnerTimeout()
    {
        CancelSpinnerTimeout();
        spinnerTimeoutCts = new CancellationTokenSource();
        var cts = spinnerTimeoutCts;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(SpinnerTimeoutSeconds), cts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (!cts.IsCancellationRequested && isBusy)
                {
                    SetIsBusy(false);

                    try
                    {
                        var state = playbackState.Value;
                        if (Schedule?.Id > 0
                            && state.IsPreparingOrPlaying
                            && state.CurrentScheduleId == Schedule.Id)
                        {
                            logger.Warning(
                                AppConstants.Logging.ScheduleListItemViewModelDiagnosticsLog.SpinnerTimedOutPlaybackActiveReRequestingModalLastResort,
                                ScheduleId);
                            WeakReferenceMessenger.Default.Send(
                                new RequestShowPlaybackModalMessage { TargetScheduleId = Schedule.Id });
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Debug(ex, AppConstants.Logging.ScheduleListItemViewModelDiagnosticsLog.ErrorCheckingPlaybackStateSpinnerTimeoutFallback);
                    }
                }
            });
        });
    }

    private void CancelSpinnerTimeout()
    {
        try
        {
            spinnerTimeoutCts?.Cancel();
            spinnerTimeoutCts?.Dispose();
        }
        catch (ObjectDisposedException ex)
        {
            Log.Logger.Debug(ex, AppConstants.Logging.ScheduleListItemViewModelDiagnosticsLog.SpinnerCancellationTokenSourceAlreadyDisposed);
        }
        finally
        {
            spinnerTimeoutCts = null;
        }
    }

    private void OnThemeChanged() => MainThread.BeginInvokeOnMainThread(() =>
    {
        OnPropertyChanged(nameof(This));
        OnPropertyChanged(nameof(IsEnabled));
    });

    public void Dispose()
    {
        CancelSpinnerTimeout();
        applicationState.StateChanged -= OnApplicationStateChanged;
        playbackState.StateChanged -= OnPlaybackStateChanged;
        WeakReferenceMessenger.Default.Unregister<ThemeChangedMessage>(this);
        WeakReferenceMessenger.Default.Unregister<RequestShowPlaybackModalMessage>(this);
        WeakReferenceMessenger.Default.Unregister<PlaybackModalOpenedMessage>(this);
        WeakReferenceMessenger.Default.Unregister<PlaybackExplicitStopMessage>(this);
    }
}
