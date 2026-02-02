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
    private const int LandscapeControlsAutoHideMs = 3000;

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
    private CancellationTokenSource? landscapeAutoHideCts;

    // Helper classes
    private readonly AlarmViewModelCommandInitializer commandInitializer;
    private readonly AlarmViewModelReviewHandler reviewHandler;
    private readonly AlarmViewModalStateUpdater stateUpdater;
    private readonly AlarmViewModalSliderHandler sliderHandler;
    private readonly AlarmViewModelArtworkHandler artworkHandler;
    private readonly ArtworkManager artworkManager;
    private readonly PositionManager positionManager;
    private readonly MessageHandler messageHandler;

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
            BeginStoppingUi);
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
            () => OnPropertyChanged(nameof(AreControlsEnabled)),
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
        RetryCommand = commandInitializer.CreateRetryCommand(() => playbackState.Value.CurrentScheduleId, () => HasError);
        sliderHandler.SetSeekCommand(SeekCommand);

        // Initialize from current state
        UpdateFromState();
        AlarmViewModelAutoDisposeMonitor.Start(playbackState, () => isDisposed, Dispose);
    }

    public bool IsLandscape => isLandscape;

    /// <summary>
    /// In landscape, playback controls auto-hide after a short delay and reappear on tap.
    /// This property controls the overlay controls visibility (progress/time + transport buttons).
    /// </summary>
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

        // Entering landscape: show controls briefly then auto-hide.
        // Leaving landscape: keep controls visible.
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

        SetLandscapeOverlayControlsVisible(true);
        ScheduleLandscapeAutoHide();
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

    private void CancelLandscapeAutoHide()
    {
        try
        {
            landscapeAutoHideCts?.Cancel();
            landscapeAutoHideCts?.Dispose();
        }
        catch
        {
            // ignore
        }
        finally
        {
            landscapeAutoHideCts = null;
        }
    }

    private void ScheduleLandscapeAutoHide()
    {
        CancelLandscapeAutoHide();

        if (!IsLandscape)
        {
            return;
        }

        var cts = new CancellationTokenSource();
        landscapeAutoHideCts = cts;
        var token = cts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(LandscapeControlsAutoHideMs, token);
            }
            catch
            {
                return;
            }

            if (token.IsCancellationRequested)
            {
                return;
            }

            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (!IsLandscape || IsStopping || IsPreparing || HasError)
                {
                    return;
                }

                SetLandscapeOverlayControlsVisible(false);
            });
        }, token);
    }

    /// <summary>
    /// Called when the user dismisses/stops playback.
    /// This is purely UI state so controls are disabled immediately while stop completes.
    /// Playback controls/progress remain visible (disabled); only the stop button swaps to a busy indicator.
    /// </summary>
    public void BeginStoppingUi()
    {
        void Apply()
        {
            if (SetProperty(ref isStopping, true, nameof(IsStopping)))
            {
                OnPropertyChanged(nameof(ShowPreparingProgress));
                OnPropertyChanged(nameof(ShowPlaybackControls));
                OnPropertyChanged(nameof(ShowLandscapeOverlayControls));
                OnPropertyChanged(nameof(AreControlsEnabled));
                OnPropertyChanged(nameof(IsStopButtonEnabled));
            }
        }

        // Important: if we're already on the UI thread, apply immediately so the spinner can render
        // before StopAsync potentially stops playback (and closes the modal).
        if (MainThread.IsMainThread)
        {
            Apply();
            return;
        }

        MainThread.BeginInvokeOnMainThread(Apply);
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
                // Update loading state when artwork source changes
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
            // Only update if user is not interacting (to prevent feedback loops)
            if (!sliderHandler.IsUserInteracting)
            {
                // Only update if value actually changed (reduces unnecessary UI work)
                if (Math.Abs(progress - value) > 0.0001) // Small threshold to avoid floating point noise
                {
                    SetProperty(ref progress, value);
                }
            }
        }
    }

    public TimeSpan Duration => currentDuration;

    /// <summary>
    /// Sets the progress value directly without checking isUserInteracting
    /// Used during drag operations to provide immediate visual feedback
    /// </summary>
    public void SetProgressDirectly(double value)
    {
        var clampedValue = Math.Max(0.0, Math.Min(1.0, value));
        if (Math.Abs(progress - clampedValue) > 0.0001)
        {
            progress = clampedValue;
            OnPropertyChanged(nameof(Progress));
        }
    }

    /// <summary>
    /// Called when user taps on the slider
    /// </summary>
    public void OnSliderTapped(double targetValue)
    {
        // Treat as an interaction so controls remain visible briefly in landscape.
        NotifyLandscapeInteraction();
        sliderHandler.OnSliderTapped(targetValue);
    }

    /// <summary>
    /// Called when user starts dragging the slider
    /// </summary>
    public void OnSliderDragStarted()
    {
        if (IsLandscape)
        {
            // Keep controls visible while dragging; don't auto-hide mid-drag.
            CancelLandscapeAutoHide();
            SetLandscapeOverlayControlsVisible(true);
        }
        sliderHandler.OnSliderDragStarted();
    }

    /// <summary>
    /// Called when user releases the slider after dragging
    /// </summary>
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
            }
        }
    }

    private bool isPreparing;
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
                OnPropertyChanged(nameof(ShowPreparingProgress));
                OnPropertyChanged(nameof(ShowMainPlayerContent));
                OnPropertyChanged(nameof(ShowLandscapeOverlayControls));
            }
        }
    }

    /// <summary>
    /// Show preparing progress when preparing tracks.
    /// </summary>
    public bool ShowPreparingProgress => IsPreparing && !HasError;

    /// <summary>
    /// Show main music player content when not preparing and there's no error.
    /// Bell fallback shows immediately if no artwork; actual artwork shows when loaded.
    /// </summary>
    public bool ShowMainPlayerContent => !IsPreparing && !HasError;

    /// <summary>
    /// Controls row visibility (progress + transport buttons).
    /// </summary>
    public bool ShowPlaybackControls => true;

    /// <summary>
    /// Controls are enabled when initial state has been received, not preparing tracks, there's no error, and not busy (dismissing)
    /// </summary>
    public bool AreControlsEnabled =>
        stateUpdater.HasReceivedInitialState &&
        !IsPreparing &&
        !HasError &&
        !IsStopping &&
        !IsBusy &&
        playbackState.Value.Status != PlayStatus.Loading;

    /// <summary>
    /// Stop button is always enabled so users can cancel downloads at any time
    /// </summary>
    public bool IsStopButtonEnabled => !IsStopping;

    public string ProgressText
    {
        get
        {
            if (totalTracks <= 0)
            {
                return "Preparing..";
            }

            if (loadedTracks < totalTracks)
            {
                // Still downloading/preparing - show percentage
                if (totalBytesDownloaded > 0 && totalBytesExpected.HasValue && totalBytesExpected.Value > 0)
                {
                    var percentage = (totalBytesDownloaded * 100.0) / totalBytesExpected.Value;
                    return $"{percentage:F1}%";
                }
                return "Preparing..";
            }

            // All tracks prepared
            return "Ready";
        }
    }

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
                OnPropertyChanged(nameof(ShowPreparingProgress));
                OnPropertyChanged(nameof(ShowMainPlayerContent));
                // Notify retry command that CanExecute may have changed
                if (RetryCommand is CommunityToolkit.Mvvm.Input.AsyncRelayCommand asyncCommand)
                {
                    asyncCommand.NotifyCanExecuteChanged();
                }
            }
        }
    }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

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
        // Handle high-frequency preparation progress updates via messaging
        // Use BeginInvokeOnMainThread to queue on UI thread without blocking
        MainThread.BeginInvokeOnMainThread(() =>
        {
            // Update download progress fields
            totalBytesDownloaded = message.TotalBytesDownloaded;
            totalBytesExpected = message.TotalBytesExpected;

            messageHandler.HandlePreparationProgressMessage(
                message,
                (loaded, total, progress, preparing) =>
                {
                    loadedTracks = loaded;
                    totalTracks = total;
                    PreparationProgress = progress;
                    
                    // When preparation is about to finish, set artwork loading to true
                    // This ensures the loading indicator shows instead of the bell placeholder
                    // until actual artwork or metadata arrives
                    if (isPreparing && !preparing)
                    {
                        IsArtworkLoading = true;
                    }
                    
                    IsPreparing = preparing;
                },
                () =>
                {
                    // Notify property changes
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


    /// <summary>
    /// Shows the Home page (sets opacity to 1.0). Called when the Playback Modal is fully rendered and visible.
    /// This ensures the Home page is visible behind the modal, preventing visual issues when the modal is dismissed.
    /// </summary>
    public void HideHomePageOverlay() => navigationService.SetHomePageVisibility(isPlaybackActive: false);

    /// <summary>
    /// When true, PlaybackModal will reveal Home (opacity=1) behind the modal after it renders.
    /// When false (e.g., cold/warm start foregrounding into active playback), Home stays hidden behind the modal.
    /// </summary>
    public bool RevealHomeBehindModalOnLoad { get; set; } = true;

    public void Dispose()
    {
        if (!isDisposed)
        {
            CancelLandscapeAutoHide();
            playbackState.StateChanged -= OnPlaybackStateChanged;
            messageHandler.UnregisterHandlers(this, this);

            isDisposed = true;
        }
    }
}

