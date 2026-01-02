#nullable enable
using System.Windows.Input;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Models;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions;
using Bible.Alarm.ViewModels.Services.Alarm;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Fluxor;
using Plugin.StoreReview;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.Shared;

public sealed class AlarmViewModal : ObservableObject, IDisposable, IRecipient<PlaybackPositionChangedMessage>, IRecipient<PlaybackPreparationProgressMessage>
{
    private readonly ILogger logger;
    private readonly IPlaybackService playbackService;
    private readonly IState<PlaybackState> playbackState;
    private readonly IDispatcher dispatcher;
    private readonly IGeneralSettingsService generalSettingsService;

    private bool isDisposed;
    private TimeSpan currentDuration = TimeSpan.Zero;

    // Helper classes
    private readonly AlarmViewModalCommandInitializer commandInitializer;
    private readonly AlarmViewModalReviewHandler reviewHandler;
    private readonly AlarmViewModalStateUpdater stateUpdater;
    private readonly AlarmViewModalSliderHandler sliderHandler;
    private readonly AlarmViewModalArtworkHandler artworkHandler;

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

    public AlarmViewModal(ILogger logger, IPlaybackService playbackService, IServiceScopeFactory scopeFactory, IState<PlaybackState> playbackState, IDispatcher dispatcher, IGeneralSettingsService generalSettingsService)
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
        reviewHandler = new AlarmViewModalReviewHandler(logger, generalSettingsService);
        commandInitializer = new AlarmViewModalCommandInitializer(logger, playbackService, reviewHandler.HandleReviewRequestAsync);
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
            (url) => UpdateArtwork(url),
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
        artworkHandler = new AlarmViewModalArtworkHandler(
            logger,
            (s) => ArtworkSource = s,
            (l) => IsArtworkLoading = l,
            () => artworkBytes = null);

        // Subscribe to Fluxor state changes for reactive updates
        playbackState.StateChanged += OnPlaybackStateChanged;

        // Subscribe to position and preparation progress messages (high-frequency updates)
        WeakReferenceMessenger.Default.Register<PlaybackPositionChangedMessage>(this);
        WeakReferenceMessenger.Default.Register<PlaybackPreparationProgressMessage>(this);

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
    // Keep bytes in memory for stream-based images
    private byte[]? artworkBytes;
    // Track last artwork URL to avoid unnecessary updates
    private string? lastArtworkUrl;

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

    public bool HasArtwork => ArtworkSource != null && !IsArtworkLoading;

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

    public bool IsPreparing
    {
        get => isPreparing;
        set
        {
            if (SetProperty(ref isPreparing, value))
            {
                OnPropertyChanged(nameof(AreControlsEnabled));
            }
        }
    }

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

    public string ProgressText => $"Preparing tracks {(totalTracks > 0 ? $"{loadedTracks}/{totalTracks}" : "")}..";

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
        if (message.CurrentPosition.HasValue && currentDuration.TotalSeconds > 0)
        {
            var actualProgress = message.CurrentPosition.Value.TotalSeconds / currentDuration.TotalSeconds;
            if (sliderHandler.ShouldIgnorePositionUpdate(actualProgress))
            {
                return;
            }
        }

        MainThread.BeginInvokeOnMainThread(() =>
        {
            UpdatePositionFromMessage(message);
            OnPropertyChanged(nameof(ProgressText));
        });
    }

    private void UpdatePositionFromMessage(PlaybackPositionChangedMessage message)
    {
        if (message.CurrentPosition.HasValue)
        {
            var position = message.CurrentPosition.Value;
            CurrentTime = $"{position.Minutes:00}:{position.Seconds:00}";

            if (currentDuration.TotalSeconds > 0)
            {
                var newProgress = position.TotalSeconds / currentDuration.TotalSeconds;
                if (Math.Abs(newProgress - progress) > 0.001)
                {
                    Progress = newProgress;
                }
            }
            else
            {
                Progress = 0.0;
            }
        }
        else
        {
            CurrentTime = "00:00";
            Progress = 0.0;
        }
    }

    private DateTime lastProgressUpdate = DateTime.MinValue;
    private const int ProgressUpdateThrottleMs = 100; // Throttle to max 10 updates per second

    public void Receive(PlaybackPreparationProgressMessage message)
    {
        // Throttle progress updates to avoid flooding UI thread
        var now = DateTime.UtcNow;
        var timeSinceLastUpdate = (now - lastProgressUpdate).TotalMilliseconds;

        if (timeSinceLastUpdate < ProgressUpdateThrottleMs && message.LoadedTracks < message.TotalTracks)
        {
            // Skip this update if it's too soon (but always process final update)
            return;
        }

        lastProgressUpdate = now;

        // Handle high-frequency preparation progress updates via messaging
        // Use BeginInvokeOnMainThread to queue on UI thread without blocking
        MainThread.BeginInvokeOnMainThread(() =>
        {
            loadedTracks = message.LoadedTracks;
            totalTracks = message.TotalTracks;
            PreparationProgress = totalTracks > 0 ? loadedTracks / (double)totalTracks : 0.0;
            IsPreparing = totalTracks > 0 && loadedTracks < totalTracks;

            // Notify property changes
            OnPropertyChanged(nameof(ProgressText));
            OnPropertyChanged(nameof(PreparationProgress));
        });
    }

    private void UpdateArtwork(string? artworkUrl)
    {
        if (lastArtworkUrl == artworkUrl)
        {
            return;
        }

        var previousUrl = lastArtworkUrl;
        lastArtworkUrl = artworkUrl;

        if (string.IsNullOrEmpty(artworkUrl))
        {
            if (artworkSource != null)
            {
                ClearArtwork();
            }
            return;
        }

        if (artworkSource != null && !string.IsNullOrEmpty(previousUrl) && previousUrl != artworkUrl)
        {
            ClearArtwork();
        }

        IsArtworkLoading = true;

        try
        {
            if (TryLoadFromUri(artworkUrl))
            {
                return;
            }

            var filePath = ResolveFilePath(artworkUrl);
            if (!string.IsNullOrEmpty(filePath))
            {
                LoadFromFile(filePath);
            }
            else
            {
                ClearArtwork();
            }
        }
        catch (Exception ex)
        {
            logger.Debug(ex, "Error updating artwork from URL: {ArtworkUrl}", artworkUrl);
            ClearArtwork();
        }
    }

    private void ClearArtwork()
    {
        ArtworkSource = null;
        artworkBytes = null;
        IsArtworkLoading = false;
        lastArtworkUrl = null;
    }

    private bool TryLoadFromUri(string artworkUrl)
    {
        if (!Uri.TryCreate(artworkUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            return false;
        }

        ArtworkSource = ImageSource.FromUri(uri);
        IsArtworkLoading = false;
        return true;
    }

    private string? ResolveFilePath(string artworkUrl)
    {
        // Handle file:// URIs
        if (artworkUrl.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                return new Uri(artworkUrl).LocalPath;
            }
            catch (Exception ex)
            {
                logger.Debug(ex, "Failed to convert file:// URI to local path: {ArtworkUrl}", artworkUrl);
                return null;
            }
        }

        // Handle direct file paths
        if (Path.IsPathRooted(artworkUrl))
        {
            return artworkUrl;
        }

        return null;
    }

    private void LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            ClearArtwork();
            return;
        }

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length == 0)
        {
            ClearArtwork();
            return;
        }

        if (DeviceInfo.Platform == DevicePlatform.Android)
        {
            LoadFromFileAndroid(filePath);
        }
        else
        {
            LoadFromFileOtherPlatforms(filePath);
        }
    }

    private void LoadFromFileAndroid(string filePath)
    {
        try
        {
            artworkBytes = File.ReadAllBytes(filePath);
            if (artworkBytes == null || artworkBytes.Length == 0)
            {
                ClearArtwork();
                return;
            }

            // Store bytes in field to keep them alive, create new stream each time
            // Capture for lambda
            var bytes = artworkBytes;
            ArtworkSource = ImageSource.FromStream(() => new MemoryStream(bytes));
            IsArtworkLoading = false;
        }
        catch (Exception ex)
        {
            logger.Debug(ex, "Failed to create ImageSource from stream for Android, trying FromFile fallback: {FilePath}", filePath);
            artworkBytes = null;
            LoadFromFileFallback(filePath);
        }
    }

    private void LoadFromFileOtherPlatforms(string filePath)
    {
        try
        {
            ArtworkSource = ImageSource.FromFile(filePath);
            IsArtworkLoading = false;
        }
        catch (Exception ex)
        {
            logger.Debug(ex, "Failed to create ImageSource from file for artwork: {FilePath}", filePath);
            ClearArtwork();
        }
    }

    private void LoadFromFileFallback(string filePath)
    {
        try
        {
            ArtworkSource = ImageSource.FromFile(filePath);
            IsArtworkLoading = false;
        }
        catch (Exception ex)
        {
            logger.Debug(ex, "Failed to create ImageSource from file for artwork (fallback): {FilePath}", filePath);
            ClearArtwork();
        }
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
            WeakReferenceMessenger.Default.Unregister<PlaybackPositionChangedMessage>(this);
            WeakReferenceMessenger.Default.Unregister<PlaybackPreparationProgressMessage>(this);


            isDisposed = true;
        }
    }
}

