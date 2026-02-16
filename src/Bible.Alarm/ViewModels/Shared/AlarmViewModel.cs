#nullable enable
using System.Windows.Input;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.ViewModels.Shared.AlarmViewModelHelpers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;
namespace Bible.Alarm.ViewModels.Shared;

public sealed class PlaybackViewModel : ObservableObject, IDisposable, IRecipient<PlaybackPositionChangedMessage>, IRecipient<PlaybackPreparationProgressMessage>
{
    private readonly ILogger logger;
    private readonly IPlaybackService playbackService;
    private readonly IState<PlaybackState> playbackState;
    private readonly IDispatcher dispatcher;
    private readonly INavigationService navigationService;
    private readonly IGeneralSettingsService generalSettingsService;

    private bool isDisposed;
    private TimeSpan currentDuration = TimeSpan.Zero;
    private bool isStopping;
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

    public ICommand PlayCommand { get; set; }
    public ICommand PauseCommand { get; set; }
    public ICommand PreviousCommand { get; set; }
    public ICommand NextCommand { get; set; }
    public ICommand ForwardCommand { get; set; }
    public ICommand BackwardCommand { get; set; }
    public ICommand SeekCommand { get; set; }
    public ICommand RetryCommand { get; set; }

    public PlaybackViewModel(ILogger logger, IPlaybackService playbackService, IServiceScopeFactory scopeFactory, IState<PlaybackState> playbackState, IDispatcher dispatcher, INavigationService navigationService, IGeneralSettingsService generalSettingsService)
    {
        this.logger = logger;
        this.playbackService = playbackService;
        this.playbackState = playbackState;
        this.dispatcher = dispatcher;
        this.navigationService = navigationService;
        this.generalSettingsService = generalSettingsService;

        // Initialize string fields to avoid nullable warnings
        title = "";
        subTitle = "";
        description = "";
        currentTime = "00:00";
        endTime = "00:00";

        // Initialize helper classes
        reviewHandler = new AlarmViewModelReviewHandler(logger, generalSettingsService);
        commandInitializer = new AlarmViewModelCommandInitializer(
            logger,
            playbackService,
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
            (url, force) => UpdateArtwork(url, force),
            () => { OnPropertyChanged(nameof(AreControlsEnabled)); OnPropertyChanged(nameof(IsBuffering)); },
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

        // Initialize commands
        DismissCommand = commandInitializer.CreateDismissCommand();
        CancelCommand = commandInitializer.CreateCancelCommand();
        PlayCommand = commandInitializer.CreatePlayCommand();
        PauseCommand = commandInitializer.CreatePauseCommand();
        PreviousCommand = commandInitializer.CreatePreviousCommand();
        NextCommand = commandInitializer.CreateNextCommand();
        ForwardCommand = commandInitializer.CreateForwardCommand();
        BackwardCommand = commandInitializer.CreateBackwardCommand();
        SeekCommand = commandInitializer.CreateSeekCommand();
        RetryCommand = commandInitializer.CreateRetryCommand(() => playbackState.Value.CurrentScheduleId, () => HasError, busy => IsRetryBusy = busy);
        sliderHandler.SetSeekCommand(SeekCommand);

        // Initialize from current state
        UpdateFromState();
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
            OnPropertyChanged(nameof(ShowPreparingProgress));
            OnPropertyChanged(nameof(ShowPreparingCard));
            OnPropertyChanged(nameof(ShowPlaybackControls));
            OnPropertyChanged(nameof(ShowLandscapeOverlayControls));
            OnPropertyChanged(nameof(AreControlsEnabled));
            OnPropertyChanged(nameof(IsBuffering));
            OnPropertyChanged(nameof(IsStopButtonEnabled));
        }
    }

    private void ApplyResetProgress()
    {
        CurrentTime = "00:00";
        SetProgressDirectly(0.0);
    }

    public bool IsStopping => isStopping;

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
    private bool isArtworkLoading;

    public ImageSource? ArtworkSource
    {
        get => artworkSource;
        private set
        {
            if (SetProperty(ref artworkSource, value))
            {
                IsArtworkLoading = false;
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

    /// <summary>Controls enabled when state received, not preparing, no error, not busy.</summary>
    public bool AreControlsEnabled =>
        stateUpdater.HasReceivedInitialState &&
        !IsPreparing &&
        !HasError &&
        !IsStopping &&
        !IsBusy &&
        playbackState.Value.Status != PlayStatus.Loading;

    /// <summary>Stop button always enabled so users can cancel downloads.</summary>
    public bool IsStopButtonEnabled => !IsStopping;

    public string ProgressText => PlaybackViewModelProgressTextHelper.GetProgressText(loadedTracks, totalTracks, totalBytesDownloaded, totalBytesExpected);

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

    private void OnPlaybackStateChanged(object? sender, EventArgs e) => UpdateFromState();

    private void UpdateFromState()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var state = playbackState.Value;
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
            messageHandler.HandlePlaybackPositionMessage(
                message,
                currentDuration,
                (progress) => sliderHandler.ShouldIgnorePositionUpdate(progress),
                (time) => CurrentTime = time,
                (progress) => Progress = progress,
                () => OnPropertyChanged(nameof(ProgressText)));
        });
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
                    if (isPreparing && !preparing)
                    {
                        IsArtworkLoading = true;
                    }

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

    private void UpdateArtwork(string? artworkUrl, bool forceReload = false)
    {
        artworkManager.UpdateArtwork(
            artworkUrl,
            (source) => ArtworkSource = source,
            (loading) => IsArtworkLoading = loading,
            forceReload);
    }


    /// <summary>Shows Home page (opacity=1). Called when Playback Modal is fully rendered.</summary>
    public void HideHomePageOverlay() => navigationService.SetHomePageVisibility(isPlaybackActive: false);

    /// <summary>When true, reveal Home behind modal after render; when false, Home stays hidden.</summary>
    public bool RevealHomeBehindModalOnLoad { get; set; } = true;

    public void Dispose()
    {
        if (!isDisposed)
        {
            landscapeHandler.CancelAutoHide();
            playbackState.StateChanged -= OnPlaybackStateChanged;
            messageHandler.UnregisterHandlers(this, this);

            isDisposed = true;
        }
    }
}

