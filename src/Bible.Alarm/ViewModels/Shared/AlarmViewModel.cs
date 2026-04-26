#nullable enable
using System.Windows.Input;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.ViewModels.Shared.AlarmViewModelHelpers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;
namespace Bible.Alarm.ViewModels.Shared;

public sealed class PlaybackViewModel : ObservableObject, IDisposable, IRecipient<PlaybackPositionChangedMessage>, IRecipient<PlaybackPreparationProgressMessage>, IRecipient<BeginStoppingPlaybackMessage>
{
    private readonly ILogger logger;
    private readonly IPlaybackService playbackService;
    private readonly IState<PlaybackState> playbackState;
    private readonly IDispatcher dispatcher;
    private readonly INavigationService navigationService;

    private bool isDisposed;
    private TimeSpan currentDuration = TimeSpan.Zero;
    private bool isStopping;
    private int? stoppingScheduleId;
    private bool isLandscape;
    private bool areLandscapeOverlayControlsVisible = true;

    // Helper classes
    private readonly AlarmViewModelCommandInitializer commandInitializer;
    private readonly AlarmViewModelReviewHandler reviewHandler;
    private readonly AlarmViewModalStateUpdater stateUpdater;
    private readonly AlarmViewModalSliderHandler sliderHandler;
    private readonly AlarmViewModelArtworkHandler artworkHandler;
    private readonly ArtworkManager artworkManager;
    private readonly PositionManager positionManager;
    private readonly MessageHandler messageHandler;
    private readonly PlaybackViewModelLandscapeHandler landscapeHandler;

    public bool IsUserInteracting => sliderHandler.IsUserInteracting;

    public ICommand DismissCommand { get; private set; }
    public ICommand CancelCommand { get; set; }
    public ICommand MinimizeCommand { get; set; }

    public ICommand PlayCommand { get; set; }
    public ICommand PauseCommand { get; set; }
    public ICommand PreviousCommand { get; set; }
    public ICommand NextCommand { get; set; }
    public ICommand ForwardCommand { get; set; }
    public ICommand BackwardCommand { get; set; }
    public ICommand SeekCommand { get; set; }
    public ICommand RetryCommand { get; set; }

    public PlaybackViewModel(ILogger logger, IPlaybackService playbackService, ISchedulePlaybackService schedulePlaybackService, IServiceScopeFactory scopeFactory, IState<PlaybackState> playbackState, IDispatcher dispatcher, INavigationService navigationService, IReviewPromptService reviewPromptService, IAudioPlayer audioPlayer)
    {
        this.logger = logger;
        this.playbackService = playbackService;
        this.playbackState = playbackState;
        this.dispatcher = dispatcher;
        this.navigationService = navigationService;

        // Initialize string fields to avoid nullable warnings
        title = "";
        subTitle = "";
        description = "";
        currentTime = "00:00";
        endTime = "00:00";

        // Initialize helper classes
        reviewHandler = new AlarmViewModelReviewHandler(logger, reviewPromptService);
        commandInitializer = new AlarmViewModelCommandInitializer(
            logger,
            playbackService,
            schedulePlaybackService,
            reviewHandler.HandleReviewRequestAsync,
            () => PlaybackViewModelStoppingHandler.BeginStoppingUi(ApplyBeginStopping),
            () => PlaybackViewModelStoppingHandler.ResetProgressUi(() => IsUserInteracting, ApplyResetProgress));
        stateUpdater = new AlarmViewModalStateUpdater(
            logger,
            (t) => Title = t,
            (s) => SubTitle = s,
            (d) => Description = d,
            (e) => EndTime = e,
            (e) => ErrorMessage = e,
            (n) => NextEnabled = n,
            (p) => PreviousEnabled = p,
            (p) => PlayVisible = p,
            (p) => PauseVisible = p,
            (d) => currentDuration = d,
            (url, fallbackUrl, force) => UpdateArtwork(url, force, fallbackUrl),
            (waiting) => IsWaitingForArtwork = waiting,
            () => { OnPropertyChanged(nameof(AreControlsEnabled)); OnPropertyChanged(nameof(IsBuffering)); OnPropertyChanged(nameof(IsShowProgressBarAnimation)); OnPropertyChanged(nameof(ShowArtworkSpinner)); },
            () => OnPropertyChanged(nameof(ProgressText)),
            () => OnPropertyChanged(nameof(PreparationProgress)),
            () => OnPropertyChanged(nameof(HasError)));
        sliderHandler = new AlarmViewModalSliderHandler(
            logger,
            () => AreControlsEnabled,
            () => currentDuration,
            SetProgressDirectly,
            () => OnPropertyChanged(nameof(Progress)));
        artworkHandler = new AlarmViewModelArtworkHandler(
            logger,
            (s) => ArtworkSource = s,
            (l) => IsArtworkLoading = l,
            () => { });

        // Initialize new helper classes
        artworkManager = new ArtworkManager(logger);
        positionManager = new PositionManager();
        messageHandler = new MessageHandler(positionManager);
        landscapeHandler = new PlaybackViewModelLandscapeHandler();

        // Subscribe to Fluxor state changes for reactive updates
        playbackState.StateChanged += OnPlaybackStateChanged;

        // Subscribe to position and preparation progress messages (high-frequency updates)
        messageHandler.RegisterHandlers(this, this);
        WeakReferenceMessenger.Default.Register<BeginStoppingPlaybackMessage>(this);

        // Initialize commands
        DismissCommand = commandInitializer.CreateDismissCommand();
        CancelCommand = commandInitializer.CreateCancelCommand();
        MinimizeCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(async () =>
        {
            IsMinimizing = true;
            await Task.Delay(100);
            WeakReferenceMessenger.Default.Send(new MinimizePlaybackMessage());
        });
        PlayCommand = commandInitializer.CreatePlayCommand();
        PauseCommand = commandInitializer.CreatePauseCommand();
        PreviousCommand = commandInitializer.CreatePreviousCommand(() =>
        {
            isTrackChangeBusy = true;
            hasSeenTrackTransition = false;
            IsPreviousBusy = true;
        });
        NextCommand = commandInitializer.CreateNextCommand(() =>
        {
            isTrackChangeBusy = true;
            hasSeenTrackTransition = false;
            IsNextBusy = true;
        });
        ForwardCommand = commandInitializer.CreateForwardCommand();
        BackwardCommand = commandInitializer.CreateBackwardCommand();
        SeekCommand = commandInitializer.CreateSeekCommand();
        RetryCommand = commandInitializer.CreateRetryCommand(() => playbackState.Value.CurrentScheduleId, () => HasError, busy => IsRetryBusy = busy);
        sliderHandler.SetSeekCommand(SeekCommand);

        // Initialize from current state
        UpdateFromState();
        InitializePositionFromAudioPlayer(audioPlayer);
        AlarmViewModelAutoDisposeMonitor.Start(playbackState, () => isDisposed, Dispose);
    }

    public bool IsLandscape => isLandscape;

    /// <summary>Landscape overlay controls visibility (progress/time + transport buttons).</summary>
    public bool AreLandscapeOverlayControlsVisible => areLandscapeOverlayControlsVisible;

    public bool ShowPortraitLayout => !IsLandscape;
    public bool ShowLandscapeLayout => IsLandscape;

    public bool ShowLandscapeOverlayControls =>
        IsLandscape &&
        AreLandscapeOverlayControlsVisible &&
        !IsPreparing &&
        !HasError;

    public void SetIsLandscape(bool value)
    {
        if (isLandscape == value)
        {
            return;
        }

        isLandscape = value;
        OnPropertyChanged(nameof(IsLandscape));
        OnPropertyChanged(nameof(ShowPortraitLayout));
        OnPropertyChanged(nameof(ShowLandscapeLayout));
        OnPropertyChanged(nameof(ShowLandscapeOverlayControls));

        if (isLandscape)
        {
            SetLandscapeOverlayControlsVisible(true);
            ScheduleLandscapeAutoHide();
        }
        else
        {
            CancelLandscapeAutoHide();
            SetLandscapeOverlayControlsVisible(true);
        }
    }

    public void NotifyLandscapeInteraction()
    {
        if (!IsLandscape || IsStopping)
        {
            return;
        }

        if (areLandscapeOverlayControlsVisible)
        {
            CancelLandscapeAutoHide();
            SetLandscapeOverlayControlsVisible(false);
        }
        else
        {
            SetLandscapeOverlayControlsVisible(true);
            ScheduleLandscapeAutoHide();
        }
    }

    private void SetLandscapeOverlayControlsVisible(bool visible)
    {
        if (areLandscapeOverlayControlsVisible == visible)
        {
            return;
        }

        areLandscapeOverlayControlsVisible = visible;
        OnPropertyChanged(nameof(AreLandscapeOverlayControlsVisible));
        OnPropertyChanged(nameof(ShowLandscapeOverlayControls));
    }

    private void CancelLandscapeAutoHide() => landscapeHandler.CancelAutoHide();

    private void ScheduleLandscapeAutoHide() =>
        landscapeHandler.ScheduleAutoHide(
            () => IsLandscape,
            () => IsStopping || IsPreparing || HasError,
            () => SetLandscapeOverlayControlsVisible(false));

    public void BeginStoppingUi() => PlaybackViewModelStoppingHandler.BeginStoppingUi(ApplyBeginStopping);

    public void ResetProgressUi() => PlaybackViewModelStoppingHandler.ResetProgressUi(() => IsUserInteracting, ApplyResetProgress);

    private void ApplyBeginStopping()
    {
        if (SetProperty(ref isStopping, true, nameof(IsStopping)))
        {
            stoppingScheduleId = playbackState.Value.CurrentScheduleId;
            OnPropertyChanged(nameof(ShowPreparingProgress));
            OnPropertyChanged(nameof(ShowPreparingCard));
            OnPropertyChanged(nameof(ShowPlaybackControls));
            OnPropertyChanged(nameof(ShowLandscapeOverlayControls));
            OnPropertyChanged(nameof(AreControlsEnabled));
            OnPropertyChanged(nameof(IsBuffering));
            OnPropertyChanged(nameof(IsStopButtonEnabled));
            OnPropertyChanged(nameof(IsShowProgressBarAnimation));
        }
    }

    private void ApplyResetProgress()
    {
        CurrentTime = "00:00";
        SetProgressDirectly(0.0);
    }

    public bool IsStopping => isStopping;

    private bool isMinimizing;
    public bool IsMinimizing
    {
        get => isMinimizing;
        set => SetProperty(ref isMinimizing, value);
    }

    private async Task ShowDismissProgress()
    {
        IsBusy = true;
        OnPropertyChanged(nameof(IsBusy));
        await Task.Delay(100);
    }

    private string title;
    public string Title
    {
        get => title;
        set => SetProperty(ref title, value);
    }

    private string subTitle;
    public string SubTitle
    {
        get => subTitle;
        set => SetProperty(ref subTitle, value);
    }

    private string description;
    public string Description
    {
        get => description;
        set => SetProperty(ref description, value);
    }

    private ImageSource? artworkSource;
    private bool isArtworkLoading = true;
    private bool isWaitingForArtwork;

    public ImageSource? ArtworkSource
    {
        get => artworkSource;
        private set
        {
            if (SetProperty(ref artworkSource, value))
            {
                OnPropertyChanged(nameof(HasArtwork));
            }
        }
    }

    public bool IsArtworkLoading
    {
        get => isArtworkLoading;
        private set
        {
            if (SetProperty(ref isArtworkLoading, value))
            {
                OnPropertyChanged(nameof(HasArtwork));
                OnPropertyChanged(nameof(ShowArtworkSpinner));
            }
        }
    }

    /// <summary>True when we have track metadata but ArtworkUrl has not been dispatched yet (show spinner until fetch/parse completes).</summary>
    public bool IsWaitingForArtwork
    {
        get => isWaitingForArtwork;
        private set
        {
            if (SetProperty(ref isWaitingForArtwork, value))
            {
                OnPropertyChanged(nameof(ShowArtworkSpinner));
            }
        }
    }

    public bool HasArtwork => artworkManager.HasArtwork;

    private bool playVisible;

    public bool PlayVisible
    {
        get => playVisible;
        set => SetProperty(ref playVisible, value);
    }

    private bool pauseVisible;

    public bool PauseVisible
    {
        get => pauseVisible;
        set => SetProperty(ref pauseVisible, value);
    }

    private string currentTime;
    public string CurrentTime
    {
        get => currentTime;
        set => SetProperty(ref currentTime, value);
    }

    private string endTime;
    public string EndTime
    {
        get => endTime;
        set => SetProperty(ref endTime, value);
    }

    private double progress;

    public double Progress
    {
        get => progress;
        set
        {
            if (!sliderHandler.IsUserInteracting)
            {
                if (Math.Abs(progress - value) > 0.0001)
                {
                    SetProperty(ref progress, value);
                }
            }
        }
    }

    public TimeSpan Duration => currentDuration;

    /// <summary>Sets progress directly (e.g. during drag) without isUserInteracting check.</summary>
    public void SetProgressDirectly(double value)
    {
        var clampedValue = Math.Max(0.0, Math.Min(1.0, value));
        if (Math.Abs(progress - clampedValue) > 0.0001)
        {
            progress = clampedValue;
            OnPropertyChanged(nameof(Progress));
        }
    }

    /// <summary>Called when user taps the slider.</summary>
    public void OnSliderTapped(double targetValue)
    {
        NotifyLandscapeInteraction();
        sliderHandler.OnSliderTapped(targetValue);
    }

    /// <summary>Called when user starts dragging the slider.</summary>
    public void OnSliderDragStarted()
    {
        if (IsLandscape)
        {
            CancelLandscapeAutoHide();
            SetLandscapeOverlayControlsVisible(true);
        }
        sliderHandler.OnSliderDragStarted();
    }

    /// <summary>Called when user releases the slider after dragging.</summary>
    public void OnSliderDragCompleted(double finalValue)
    {
        sliderHandler.OnSliderDragCompleted(finalValue);
        if (IsLandscape && !IsStopping)
        {
            ScheduleLandscapeAutoHide();
        }
    }

    private bool nextEnabled;

    public bool NextEnabled
    {
        get => nextEnabled;
        set => SetProperty(ref nextEnabled, value);
    }

    private bool previousEnabled;

    public bool PreviousEnabled
    {
        get => previousEnabled;
        set => SetProperty(ref previousEnabled, value);
    }

    private bool isBusy;

    public bool IsBusy
    {
        get => isBusy;
        set
        {
            if (SetProperty(ref isBusy, value))
            {
                OnPropertyChanged(nameof(AreControlsEnabled));
                OnPropertyChanged(nameof(IsBuffering));
                OnPropertyChanged(nameof(IsShowProgressBarAnimation));
            }
        }
    }

    private bool isPreparing;
    private bool showPreparationPercent;
    private int loadedTracks;
    private int totalTracks;
    private long totalBytesDownloaded;
    private long? totalBytesExpected;

    public bool IsPreparing
    {
        get => isPreparing;
        set
        {
            if (SetProperty(ref isPreparing, value))
            {
                OnPropertyChanged(nameof(AreControlsEnabled));
                OnPropertyChanged(nameof(IsBuffering));
                OnPropertyChanged(nameof(IsShowProgressBarAnimation));
                OnPropertyChanged(nameof(ShowArtworkSpinner));
                OnPropertyChanged(nameof(ShowPreparingProgress));
                OnPropertyChanged(nameof(ShowPreparingCard));
                OnPropertyChanged(nameof(ShowMainPlayerContent));
                OnPropertyChanged(nameof(ShowLandscapeOverlayControls));
            }
        }
    }

    /// <summary>Show preparing progress when preparing tracks.</summary>
    public bool ShowPreparingProgress => IsPreparing && !HasError;

    /// <summary>Show progress percent and bar only when API fetch is involved (e.g. section change). False for stream/cache.</summary>
    public bool ShowPreparationPercent => showPreparationPercent;

    /// <summary>Show the preparing card only when API fetch is involved (section change). Hidden for stream/cache to avoid momentary flash.</summary>
    public bool ShowPreparingCard => ShowPreparingProgress && ShowPreparationPercent;

    /// <summary>Show main player content when not preparing and no error.</summary>
    public bool ShowMainPlayerContent => !IsPreparing && !HasError;

    public bool ShowPlaybackControls => true;

    /// <summary>True when media is buffering mid-playback (not during initial track preparation).</summary>
    public bool IsBuffering =>
        !IsPreparing &&
        !HasError &&
        !IsStopping &&
        playbackState.Value.Status == PlayStatus.Loading;

    /// <summary>True when progress bar animation should show: buffering or track transition (Next/Previous fetch).</summary>
    public bool IsShowProgressBarAnimation =>
        !IsPreparing &&
        !HasError &&
        !IsStopping &&
        (playbackState.Value.Status == PlayStatus.Loading || playbackState.Value.IsTransitioningTrack);

    public bool ShowArtworkSpinner => playbackState.Value.IsTransitioningTrack || IsArtworkLoading || IsWaitingForArtwork;

    /// <summary>Controls enabled when state received, not preparing, no error, not busy.</summary>
    public bool AreControlsEnabled =>
        stateUpdater.HasReceivedInitialState &&
        !IsPreparing &&
        !HasError &&
        !IsStopping &&
        !IsBusy &&
        playbackState.Value.Status != PlayStatus.Loading &&
        playbackState.Value.Status != PlayStatus.Stopped;

    /// <summary>Stop button is always enabled (escape hatch for user to dismiss).</summary>
    public bool IsStopButtonEnabled => true;

    public string ProgressText => PlaybackViewModelProgressTextHelper.GetProgressText(loadedTracks, totalTracks, totalBytesDownloaded, totalBytesExpected, PreparationProgress);

    public double PreparationProgress { get; private set; }

    private string errorMessage = string.Empty;

    public string ErrorMessage
    {
        get => errorMessage;
        private set
        {
            if (SetProperty(ref errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
                OnPropertyChanged(nameof(AreControlsEnabled));
                OnPropertyChanged(nameof(IsBuffering));
                OnPropertyChanged(nameof(IsShowProgressBarAnimation));
                OnPropertyChanged(nameof(ShowArtworkSpinner));
                OnPropertyChanged(nameof(ShowPreparingProgress));
                OnPropertyChanged(nameof(ShowPreparingCard));
                OnPropertyChanged(nameof(ShowMainPlayerContent));
                if (RetryCommand is CommunityToolkit.Mvvm.Input.AsyncRelayCommand asyncCommand)
                {
                    asyncCommand.NotifyCanExecuteChanged();
                }
            }
        }
    }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    private bool isRetryBusy;

    public bool IsRetryBusy
    {
        get => isRetryBusy;
        set => SetProperty(ref isRetryBusy, value);
    }

    private bool isPreviousBusy;
    public bool IsPreviousBusy
    {
        get => isPreviousBusy;
        set
        {
            if (SetProperty(ref isPreviousBusy, value))
            {
                OnPropertyChanged(nameof(ShowPreviousButton));
            }
        }
    }

    public bool ShowPreviousButton => !IsPreviousBusy;

    private bool isNextBusy;
    public bool IsNextBusy
    {
        get => isNextBusy;
        set
        {
            if (SetProperty(ref isNextBusy, value))
            {
                OnPropertyChanged(nameof(ShowNextButton));
            }
        }
    }

    public bool ShowNextButton => !IsNextBusy;

    private bool isTrackChangeBusy;
    private bool hasSeenTrackTransition;

    /// <summary>
    /// Syncs the slider and current-time label with the audio player's position.
    /// Called once during construction so the modal shows the correct progress
    /// when re-opened after being minimized while paused (no position messages
    /// are sent while paused, so without this the slider would start at 0%).
    /// </summary>
    private void InitializePositionFromAudioPlayer(IAudioPlayer audioPlayer)
    {
        var position = audioPlayer.CurrentPosition;
        var duration = audioPlayer.Duration;
        if (position.HasValue && duration > TimeSpan.Zero)
        {
            CurrentTime = PositionManager.FormatTime(position.Value);
            SetProgressDirectly(position.Value.TotalSeconds / duration.TotalSeconds);
        }
    }

    private void OnPlaybackStateChanged(object? sender, EventArgs e) => UpdateFromState();

    /// <summary>
    /// Returns false when the UI should ignore this state tick while <see cref="isStopping"/> is true.
    /// Clears stopping only when a different schedule begins loading; intermediate Loading for the
    /// same schedule after dismiss must not re-enable controls (<see cref="stoppingScheduleId"/>).
    /// </summary>
    private bool TryExitStoppingStateForNewScheduleLoading(PlaybackState state)
    {
        if (!isStopping)
        {
            return true;
        }

        if (state.Status == PlayStatus.Loading
            && state.CurrentScheduleId.HasValue
            && state.CurrentScheduleId != stoppingScheduleId)
        {
            isStopping = false;
            stoppingScheduleId = null;
            OnPropertyChanged(nameof(IsStopping));
            OnPropertyChanged(nameof(ShowPreparingProgress));
            OnPropertyChanged(nameof(ShowPreparingCard));
            OnPropertyChanged(nameof(ShowPlaybackControls));
            OnPropertyChanged(nameof(ShowLandscapeOverlayControls));
            OnPropertyChanged(nameof(AreControlsEnabled));
            OnPropertyChanged(nameof(IsBuffering));
            OnPropertyChanged(nameof(IsStopButtonEnabled));
            OnPropertyChanged(nameof(IsShowProgressBarAnimation));
            return true;
        }

        return false;
    }

    private void ClearTrackChangeBusyFlags()
    {
        isTrackChangeBusy = false;
        hasSeenTrackTransition = false;
        IsPreviousBusy = false;
        IsNextBusy = false;
    }

    private void UpdateTrackChangeBusyFromPlaybackState(PlaybackState state)
    {
        if (!isTrackChangeBusy)
        {
            return;
        }

        if (!hasSeenTrackTransition)
        {
            if (state.Status != PlayStatus.Playing && state.Status != PlayStatus.Paused)
            {
                hasSeenTrackTransition = true;
            }

            return;
        }

        if (state.Status is PlayStatus.Playing or PlayStatus.Paused)
        {
            ClearTrackChangeBusyFlags();
            return;
        }

        if (state.Status is PlayStatus.Failed or PlayStatus.Ended
            || (state.Status == PlayStatus.Stopped
                && !state.IsTransitioningTrack && !state.IsAutoAdvancing))
        {
            ClearTrackChangeBusyFlags();
        }
    }

    private void UpdateFromState()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var state = playbackState.Value;

            if (!TryExitStoppingStateForNewScheduleLoading(state))
            {
                return;
            }

            UpdateTrackChangeBusyFromPlaybackState(state);

            var trackChanged = stateUpdater.DetectTrackChange(state);
            stateUpdater.HandleTrackChange(trackChanged, state);
            stateUpdater.UpdateControlsFromState(state);
            stateUpdater.UpdateMetadataFromState(state, trackChanged);
            stateUpdater.UpdatePlaybackStateFromState(state);
        });
    }

    public void Receive(PlaybackPositionChangedMessage message)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            // While stopping, ignore position updates so the progress bar stays frozen.
            // This prevents the slider from visually moving after dismiss is tapped.
            // Safe for schedule switching: isStopping is cleared before the new schedule's
            // position messages arrive (cleared in UpdateFromState when PlayStatus.Loading && HasValue).
            if (isStopping)
            {
                return;
            }

            messageHandler.HandlePlaybackPositionMessage(
                message,
                currentDuration,
                (progress) => sliderHandler.ShouldIgnorePositionUpdate(progress),
                (time) => CurrentTime = time,
                (progress) => Progress = progress,
                () => OnPropertyChanged(nameof(ProgressText)));
        });
    }


    public void Receive(BeginStoppingPlaybackMessage message)
    {
        if (MainThread.IsMainThread)
        {
            BeginStoppingUi();
        }
        else
        {
            MainThread.BeginInvokeOnMainThread(() => BeginStoppingUi());
        }
    }

    public void Receive(PlaybackPreparationProgressMessage message)
    {
        var showPercentForThisMessage = message.ShowPercent;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (showPercentForThisMessage)
            {
                if (!showPreparationPercent)
                {
                    showPreparationPercent = true;
                    OnPropertyChanged(nameof(ShowPreparationPercent));
                    OnPropertyChanged(nameof(ShowPreparingCard));
                }
            }
            else
            {
                if (showPreparationPercent)
                {
                    showPreparationPercent = false;
                    OnPropertyChanged(nameof(ShowPreparationPercent));
                    OnPropertyChanged(nameof(ShowPreparingCard));
                }
            }

            totalBytesDownloaded = message.TotalBytesDownloaded;
            totalBytesExpected = message.TotalBytesExpected;

            messageHandler.HandlePreparationProgressMessage(
                message,
                (loaded, total, progress, preparing) =>
                {
                    loadedTracks = loaded;
                    totalTracks = total;
                    PreparationProgress = progress;
                    // Do not set IsArtworkLoading = true here. Artwork loading is driven by UpdateArtwork
                    // when state.ArtworkUrl or default is set. Setting it here when preparation ends caused
                    // an endless spinner when artwork extraction failed or timed out (no URL dispatched).

                    if (preparing && !isPreparing && !showPercentForThisMessage)
                    {
                        showPreparationPercent = false;
                        OnPropertyChanged(nameof(ShowPreparationPercent));
                        OnPropertyChanged(nameof(ShowPreparingCard));
                    }
                    else if (!preparing && showPreparationPercent)
                    {
                        showPreparationPercent = false;
                        OnPropertyChanged(nameof(ShowPreparationPercent));
                        OnPropertyChanged(nameof(ShowPreparingCard));
                    }

                    IsPreparing = preparing;
                },
                () =>
                {
                    OnPropertyChanged(nameof(ProgressText));
                    OnPropertyChanged(nameof(PreparationProgress));
                });
        });
    }

    private void UpdateArtwork(string? artworkUrl, bool forceReload = false, string? fallbackUrl = null)
    {
        artworkManager.UpdateArtwork(
            artworkUrl,
            (source) => ArtworkSource = source,
            (loading) => IsArtworkLoading = loading,
            forceReload,
            fallbackUrl);
    }


    public int? CurrentScheduleId => playbackState.Value.CurrentScheduleId;

    public void Dispose()
    {
        if (!isDisposed)
        {
            landscapeHandler.CancelAutoHide();
            playbackState.StateChanged -= OnPlaybackStateChanged;
            messageHandler.UnregisterHandlers(this, this);
            WeakReferenceMessenger.Default.Unregister<BeginStoppingPlaybackMessage>(this);

            isDisposed = true;
        }
    }
}
