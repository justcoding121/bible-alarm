#nullable enable

using System.Collections.ObjectModel;
using System.Windows.Input;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.Shared;
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
    private bool hasSignaledReady;
    private bool isReadyActionQueued;
    private string? lastPublicationCode;

    private ObservableCollection<NumberOfTracksListViewItemModel> numberOfTracksList = new();
    private NumberOfTracksListViewItemModel? currentNumberOfTracks;

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
                DispatchScheduleUpdate(s => s.NumberOfTracksToRead = CurrentNumberOfTracks.Value);
            }

            // Explicitly notify property changes to ensure UI binding updates
            OnPropertyChanged(nameof(CurrentNumberOfTracks));
            OnPropertyChanged(nameof(CurrentNumberOfTracksText));

            await navigationService.PopModalAsync();
        });

        ToggleAlwaysPlayFromStartCommand = new RelayCommand(() => AlwaysPlayFromStart = !AlwaysPlayFromStart);

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
            lastPublicationCode = currentSchedule.BiblePublicationCode;

            PopulateNumberOfTracksListView();

            OnPropertyChanged(nameof(NotificationEnabled));
            OnPropertyChanged(nameof(AlwaysPlayFromStart));
            OnPropertyChanged(nameof(HasSectionStructure));
            OnPropertyChanged(nameof(TrackLabelText));
            OnPropertyChanged(nameof(ModalHeaderText));
            OnPropertyChanged(nameof(RestartLabelText));

            // Signal that this container is ready (initialized from CurrentSchedule)
            SignalContainerReady();
        }
    }

    private void SignalContainerReady()
    {
        // Check if already signaled or already marked ready in state
        // This check must happen first to prevent any duplicate work
        if (hasSignaledReady || state.Value.ContainerReadiness.NumberOfTrack) return;

        // Check if action is already queued to prevent duplicate queued actions
        // This prevents multiple rapid calls from queuing multiple actions
        if (isReadyActionQueued) return;

        // Atomically set both flags to prevent race conditions
        // If another thread/call checks between these lines, it will see isReadyActionQueued=true
        isReadyActionQueued = true;
        hasSignaledReady = true;

        // Double-check state immediately after setting flags (before queuing)
        // This catches the case where state changed between the initial check and flag setting
        if (state.Value.ContainerReadiness.NumberOfTrack)
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
            if (state.Value.ContainerReadiness.NumberOfTrack)
            {
                // Ensure flag is set to prevent future attempts
                hasSignaledReady = true;
                return;
            }
            dispatcher.Dispatch(new ContainerReadyAction("NumberOfTrack"));
        });
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        var stateValue = state.Value;
        var currentSchedule = stateValue.CurrentSchedule;

        // If ContainerReadiness was reset to NotReady but we've already signaled ready, reset our flag
        // This handles the case where ViewScheduleAction resets ContainerReadiness after containers signaled ready
        if (hasSignaledReady && !stateValue.ContainerReadiness.NumberOfTrack && currentSchedule != null)
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

            // Check if publication code changed (switched between sectioned and non-sectioned)
            var newPublicationCode = currentSchedule.BiblePublicationCode;
            if (lastPublicationCode != null && lastPublicationCode != newPublicationCode)
            {
                var wasHasSectionStructure = PublicationTypeHelper.HasSectionStructure(lastPublicationCode);
                var nowHasSectionStructure = PublicationTypeHelper.HasSectionStructure(newPublicationCode);

                if (wasHasSectionStructure != nowHasSectionStructure)
                {
                    // Update label text properties
                    OnPropertyChanged(nameof(HasSectionStructure));
                    OnPropertyChanged(nameof(TrackLabelText));
                    OnPropertyChanged(nameof(ModalHeaderText));
                    OnPropertyChanged(nameof(RestartLabelText));

                    // Set appropriate default: 3 for chapters (sectioned), 1 for episodes (non-sectioned)
                    var newDefault = nowHasSectionStructure ? 3 : 1;
                    
                    // Repopulate the list to update max for non-sectioned publications
                    // Pass the new default as forced selection so it's selected when list is populated
                    PopulateNumberOfTracksListView(newDefault);
                    
                    // Dispatch update to state
                    DispatchScheduleUpdate(s => s.NumberOfTracksToRead = newDefault);
                }
            }
            lastPublicationCode = newPublicationCode;
        }
    }

    public ICommand OpenModalCommand { get; private set; } = null!;
    public ICommand SelectNumberOfTracksCommand { get; private set; } = null!;
    public ICommand ToggleAlwaysPlayFromStartCommand { get; private set; } = null!;
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
            }
        }
    }

    /// <summary>
    /// Computed property for binding to the number of tracks text in the UI.
    /// This ensures the UI updates when CurrentNumberOfTracks changes.
    /// </summary>
    public string CurrentNumberOfTracksText => CurrentNumberOfTracks?.Text ?? string.Empty;

    /// <summary>
    /// Gets whether the current publication has section structure (traditional Bible with chapters).
    /// Non-sectioned publications are dramas/videos with episodes.
    /// </summary>
    public bool HasSectionStructure
    {
        get
        {
            var publicationCode = state.Value.CurrentSchedule?.BiblePublicationCode;
            return PublicationTypeHelper.HasSectionStructure(publicationCode);
        }
    }

    /// <summary>
    /// Gets the label text for the tracks selection row.
    /// Returns "Chapters to play each time" for sectioned publications (Bible),
    /// or "Episodes to play each time" for non-sectioned publications (dramas).
    /// </summary>
    public string TrackLabelText => HasSectionStructure ? "Chapters to play each time" : "Episodes to play each time";

    /// <summary>
    /// Gets the header text for the tracks selection modal.
    /// Returns "Select Number of Chapters" for sectioned publications,
    /// or "Select Number of Episodes" for non-sectioned publications.
    /// </summary>
    public string ModalHeaderText => HasSectionStructure ? "Select Number of Chapters" : "Select Number of Episodes";

    /// <summary>
    /// Gets the label text for the "restart incomplete" toggle.
    /// Returns "Restart incomplete chapters from the beginning?" for sectioned publications,
    /// or "Restart incomplete episodes from the beginning?" for non-sectioned publications.
    /// </summary>
    public string RestartLabelText => HasSectionStructure 
        ? "Restart incomplete chapters from the beginning?" 
        : "Restart incomplete episodes from the beginning?";

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
                                // Permission denied - revert the toggle
                                logger.Warning("Notification permission denied - reverting toggle");
                                MainThread.BeginInvokeOnMainThread(() =>
                                {
                                    notificationEnabled = false;
                                    OnPropertyChanged(nameof(NotificationEnabled));
                                });

                                // Show message to user
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
                                logger.Information("Notification permission granted - toggle remains enabled");
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.Error(ex, "Error requesting notification permission");
                        }
                    });
#endif
                }

                DispatchScheduleUpdate(s => s.NotificationEnabled = value);
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

    private async void PopulateNumberOfTracksListView(int? forceSelection = null)
    {
        // Preserve the current selection if user has made one, or use forced selection
        var preservedSelection = forceSelection ?? CurrentNumberOfTracks?.Value;
        var currentSchedule = state.Value.CurrentSchedule;
        var hasSectionStructure = HasSectionStructure;

        // Default: 3 for chapters (sectioned), 1 for episodes (non-sectioned)
        var defaultTracks = hasSectionStructure ? 3 : 1;
        var numberOfTracks = preservedSelection ?? currentSchedule?.NumberOfTracksToRead ?? defaultTracks;

        // Determine maximum number of tracks to show
        int maxTracks = 21; // Default for sectioned publications
        
        // For non-sectioned publications, get the actual number of episodes
        if (!hasSectionStructure && currentSchedule != null)
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
                        maxTracks = publication.Tracks.Count;
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
            var tracksVm = new NumberOfTracksListViewItemModel(i, hasSectionStructure);

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
    /// Updates the unit labels on all list items when publication type changes.
    /// </summary>
    private void UpdateListItemLabels()
    {
        var hasSectionStructure = HasSectionStructure;
        foreach (var item in NumberOfTracksList)
        {
            item.UpdateUnitLabels(hasSectionStructure);
        }
        OnPropertyChanged(nameof(CurrentNumberOfTracksText));
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
            NumberOfTracksToRead = source.NumberOfTracksToRead,
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

