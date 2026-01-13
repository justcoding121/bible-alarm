#nullable enable
using System.Windows.Input;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions;
using Bible.Alarm.ViewModels.Shared.AlarmViewModelHelpers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.Shared;

public sealed class AlarmViewModel : ObservableObject, IDisposable, IRecipient<PlaybackPositionChangedMessage>, IRecipient<PlaybackPreparationProgressMessage>
{
    private readonly ILogger logger;
    private readonly IPlaybackService playbackService;
    private readonly IState<PlaybackState> playbackState;
    private readonly IDispatcher dispatcher;
    private readonly IGeneralSettingsService generalSettingsService;

    private bool isDisposed;
    private TimeSpan currentDuration = TimeSpan.Zero;

    // Helper classes
    private readonly AlarmViewModelCommandInitializer commandInitializer;
    private readonly AlarmViewModelReviewHandler reviewHandler;
    private readonly AlarmViewModalStateUpdater stateUpdater;
    private readonly AlarmViewModalSliderHandler sliderHandler;
    private readonly AlarmViewModelArtworkHandler artworkHandler;
    private readonly ArtworkManager artworkManager;
    private readonly PositionManager positionManager;
    private readonly MessageHandler messageHandler;

    /// <summary>
    /// Public property to allow code-behind to check if user is interacting
    /// </summary>
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

    public AlarmViewModel(ILogger logger, IPlaybackService playbackService, IServiceScopeFactory scopeFactory, IState<PlaybackState> playbackState, IDispatcher dispatcher, IGeneralSettingsService generalSettingsService)
    {
        this.logger = logger;
        this.playbackService = playbackService;
        this.playbackState = playbackState;
        this.dispatcher = dispatcher;
        this.generalSettingsService = generalSettingsService;

        // Initialize string fields to avoid nullable warnings
        title = "";
        subTitle = "";
        description = "";
        currentTime = "00:00";
        endTime = "00:00";

        // Initialize helper classes
        reviewHandler = new AlarmViewModelReviewHandler(logger, generalSettingsService);
        commandInitializer = new AlarmViewModelCommandInitializer(logger, playbackService, reviewHandler.HandleReviewRequestAsync);
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

        // NOTE:
        // Do not reset Title/SubTitle/Description/etc here.
        // UpdateFromState() already hydrated the VM from Fluxor, and resetting afterwards
        // causes the UI to appear blank (especially noticeable in Android Auto -> later open app flows).

        Task.Run(async () =>
        {
            while (!isDisposed)
            {
                await Task.Delay(1000);

                var isRunning = playbackState.Value.IsPreparingOrPlaying;

                var count = 6;
                while (!isRunning && count > 0)
                {
                    await Task.Delay(500);
                    isRunning = playbackState.Value.IsPreparingOrPlaying;
                    count--;
                }

                if (!isRunning)
                {
                    Dispose();
                }
            }
        });
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
        sliderHandler.OnSliderTapped(targetValue);
    }

    /// <summary>
    /// Called when user starts dragging the slider
    /// </summary>
    public void OnSliderDragStarted()
    {
        sliderHandler.OnSliderDragStarted();
    }

    /// <summary>
    /// Called when user releases the slider after dragging
    /// </summary>
    public void OnSliderDragCompleted(double finalValue)
    {
        sliderHandler.OnSliderDragCompleted(finalValue);
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
    private long bytesDownloaded;
    private long? totalBytes;
    private double currentTrackProgress;

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
    /// Controls are enabled when initial state has been received, not preparing tracks, there's no error, and not busy (dismissing)
    /// </summary>
    public bool AreControlsEnabled =>
        stateUpdater.HasReceivedInitialState &&
        !IsPreparing &&
        !HasError &&
        !IsBusy &&
        playbackState.Value.Status != PlayStatus.Loading;

    /// <summary>
    /// Stop button is always enabled so users can cancel downloads at any time
    /// </summary>
    public bool IsStopButtonEnabled => true;

    public string ProgressText
    {
        get
        {
            if (totalTracks <= 0)
            {
                return "Preparing..\n ";
            }

            // Always use two lines to prevent layout jumps when text changes
            // Line 1: Track progress
            // Line 2: Download percentage (or empty placeholder)
            
            if (loadedTracks < totalTracks)
            {
                // Still downloading/preparing
                var downloadInfo = " "; // Placeholder to maintain height
                if (bytesDownloaded > 0 && totalBytes.HasValue && totalBytes.Value > 0)
                {
                    var percentage = (bytesDownloaded * 100.0) / totalBytes.Value;
                    downloadInfo = $"{percentage:F1}%";
                }
                return $"Downloading track {loadedTracks + 1}/{totalTracks}\n{downloadInfo}";
            }

            // All tracks prepared
            return $"Prepared {totalTracks} tracks\n ";
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
            bytesDownloaded = message.BytesDownloaded;
            totalBytes = message.TotalBytes;
            currentTrackProgress = message.CurrentTrackProgress;

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
    /// Hides the Home page overlay. Called when the Alarm Modal is fully rendered and visible.
    /// </summary>
    public void HideHomePageOverlay() => dispatcher.Dispatch(new SetHomePageOverlayAction { IsVisible = false });

    public void Dispose()
    {
        if (!isDisposed)
        {
            playbackState.StateChanged -= OnPlaybackStateChanged;
            messageHandler.UnregisterHandlers(this, this);

            isDisposed = true;
        }
    }
}

