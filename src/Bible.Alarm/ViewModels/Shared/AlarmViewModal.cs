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
    private bool hasReceivedInitialState;
    private string? previousTrackTitle;
    private string? previousTrackArtist;
    private string? previousTrackAlbum;
    private string? previousArtworkUrl;
    private TimeSpan currentDuration = TimeSpan.Zero;
    // True when user is dragging or tapping the slider
    private bool isUserInteracting;
    // Track where user wants to seek to
    private double? targetSeekProgress;

    /// <summary>
    /// Public property to allow code-behind to check if user is interacting
    /// </summary>
    public bool IsUserInteracting => isUserInteracting;

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

        // Controls are hidden until first playback state is received
        hasReceivedInitialState = false;

        // Subscribe to Fluxor state changes for reactive updates
        playbackState.StateChanged += OnPlaybackStateChanged;

        // Subscribe to position and preparation progress messages (high-frequency updates)
        WeakReferenceMessenger.Default.Register<PlaybackPositionChangedMessage>(this);
        WeakReferenceMessenger.Default.Register<PlaybackPreparationProgressMessage>(this);

        // Initialize from current state
        UpdateFromState();

        DismissCommand = new AsyncRelayCommand(async () =>
        {
            await ShowDismissProgress();
            await playbackService.StopAsync();
            await HandleReviewRequest();
        });

        CancelCommand = new RelayCommand(() =>
        {
            // This command doesn't need navigation - the modal will be closed when playback is dismissed
        });

        PlayCommand = new AsyncRelayCommand(async () =>
        {
            await playbackService.PlayAsync();
        });

        PauseCommand = new AsyncRelayCommand(async () =>
        {
            await playbackService.PauseAsync();
        });

        PreviousCommand = new AsyncRelayCommand(async () =>
        {
            await playbackService.PlayPreviousAsync();
        });

        NextCommand = new AsyncRelayCommand(async () =>
        {
            await playbackService.PlayNextAsync();
        });

        ForwardCommand = new AsyncRelayCommand(async () =>
        {
            await playbackService.SeekForwardAsync();
        });

        BackwardCommand = new AsyncRelayCommand(async () =>
        {
            await playbackService.SeekBackwardAsync();
        });

        SeekCommand = new AsyncRelayCommand<TimeSpan>(async position =>
        {
            await playbackService.SeekToAsync(position);
        });

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

    private async Task HandleReviewRequest()
    {
        try
        {
            await Task.Run(async () =>
            {
                if (!await generalSettingsService.GeneralSettingExistsAsync(
                        AppConstants.GeneralSettingsKeys.ReviewRequested))
                {
                    await ProcessDismissCount();
                }
            });
        }
        catch (Exception e)
        {
            logger.Error(e, "An error happened when review was requested.");
        }
    }

    private async Task ProcessDismissCount()
    {
        var dismissCount = await generalSettingsService.GetGeneralSettingAsync(
            AppConstants.GeneralSettingsKeys.DismissCount);

        if (dismissCount != null && dismissCount.Value != null && int.Parse(dismissCount.Value) >= 6)
        {
            await RequestReview();
        }
        else
        {
            await IncrementDismissCount(dismissCount);
        }
    }

    private async Task RequestReview()
    {
        await generalSettingsService.SetGeneralSettingAsync(
            AppConstants.GeneralSettingsKeys.ReviewRequested,
            "True");

        await MainThread.InvokeOnMainThreadAsync(async () =>
            await CrossStoreReview.Current.RequestReview(false));
    }

    private async Task IncrementDismissCount(GeneralSettings? dismissCount)
    {
        if (dismissCount?.Value != null)
        {
            await generalSettingsService.SetGeneralSettingAsync(
                AppConstants.GeneralSettingsKeys.DismissCount,
                (int.Parse(dismissCount.Value) + 1).ToString());
        }
        else
        {
            await generalSettingsService.SetGeneralSettingAsync(
                AppConstants.GeneralSettingsKeys.DismissCount,
                "1");
        }
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
            if (!isUserInteracting)
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
    /// Called when user taps on the slider
    /// </summary>
    public void OnSliderTapped(double targetValue)
    {
        logger?.Debug("[Slider] Tap detected - TargetValue: {TargetValue}", targetValue);

        if (!AreControlsEnabled || currentDuration.TotalSeconds <= 0)
        {
            return;
        }

        // Clamp value to valid range (0.0 to 1.0)
        var progress = Math.Max(0.0, Math.Min(1.0, targetValue));
        targetSeekProgress = progress;

        // Update visual position directly (bypass Progress setter to avoid feedback loop)
        isUserInteracting = true;
        this.progress = progress;
        OnPropertyChanged(nameof(Progress));

        // Perform seek immediately for tap
        PerformSeek();
    }

    /// <summary>
    /// Called when user starts dragging the slider
    /// </summary>
    public void OnSliderDragStarted()
    {
        isUserInteracting = true;
        logger?.Debug("[Slider] Drag started");
    }

    /// <summary>
    /// Called when user releases the slider after dragging
    /// </summary>
    public void OnSliderDragCompleted(double finalValue)
    {
        logger?.Debug("[Slider] Drag completed - FinalValue: {FinalValue}", finalValue);

        if (!AreControlsEnabled || currentDuration.TotalSeconds <= 0)
        {
            isUserInteracting = false;
            return;
        }

        // Update target and perform seek immediately
        var progress = Math.Max(0.0, Math.Min(1.0, finalValue));
        targetSeekProgress = progress;

        // Update visual position (bypass Progress setter to avoid feedback loop)
        this.progress = progress;
        OnPropertyChanged(nameof(Progress));

        PerformSeek();
    }

    /// <summary>
    /// Performs the actual seek operation to the target position
    /// </summary>
    private void PerformSeek()
    {
        if (!targetSeekProgress.HasValue || !AreControlsEnabled || currentDuration.TotalSeconds <= 0)
        {
            isUserInteracting = false;
            targetSeekProgress = null;
            return;
        }

        try
        {
            var seekPosition = TimeSpan.FromSeconds(currentDuration.TotalSeconds * targetSeekProgress.Value);
            logger?.Debug("[Slider] Performing seek to position: {Position}, Progress: {Progress}",
                seekPosition, targetSeekProgress.Value);

            if (SeekCommand != null && SeekCommand.CanExecute(seekPosition))
            {
                SeekCommand.Execute(seekPosition);
            }

            // Reset interaction flag after delay to allow seek to complete
            Task.Delay(500).ContinueWith(_ =>
            {
                isUserInteracting = false;
                targetSeekProgress = null;
                logger?.Debug("[Slider] User interaction ended");
            });
        }
        catch (Exception ex)
        {
            logger?.Error(ex, "[Slider] Error performing seek");
            isUserInteracting = false;
            targetSeekProgress = null;
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
        hasReceivedInitialState &&
        !IsPreparing &&
        !HasError &&
        !IsBusy &&
        playbackState.Value.Status != PlayStatus.Loading;

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
            var trackChanged = DetectTrackChange(state);
            HandleTrackChange(trackChanged, state);
            UpdateControlsFromState(state);
            UpdateMetadataFromState(state, trackChanged);
            UpdatePlaybackStateFromState(state);
        });
    }

    private bool DetectTrackChange(PlaybackState state)
    {
        var currentTitle = state.Title ?? "";
        var currentArtist = state.Artist ?? "";
        var currentAlbum = state.Album ?? "";

        return hasReceivedInitialState &&
               (previousTrackTitle != currentTitle ||
                previousTrackArtist != currentArtist ||
                previousTrackAlbum != currentAlbum);
    }

    private void HandleTrackChange(bool trackChanged, PlaybackState state)
    {
        if (trackChanged)
        {
            hasReceivedInitialState = false;
            OnPropertyChanged(nameof(AreControlsEnabled));
        }

        if (!hasReceivedInitialState &&
            (state.Status == PlayStatus.Playing || state.Status == PlayStatus.Paused))
        {
            hasReceivedInitialState = true;
            OnPropertyChanged(nameof(AreControlsEnabled));
        }

        if (hasReceivedInitialState)
        {
            previousTrackTitle = state.Title ?? "";
            previousTrackArtist = state.Artist ?? "";
            previousTrackAlbum = state.Album ?? "";
        }
    }

    private void UpdateControlsFromState(PlaybackState state)
    {
        NextEnabled = state.CanPlayNext;
        PreviousEnabled = state.CanPlayPrevious;
    }

    private void UpdateMetadataFromState(PlaybackState state, bool trackChanged)
    {
        Title = state.Title ?? "";
        SubTitle = state.Artist ?? "";
        Description = state.Album ?? "";

        var artworkUrl = state.ArtworkUrl;
        var shouldForceUpdate = trackChanged || (previousArtworkUrl != artworkUrl);
        if (shouldForceUpdate)
        {
            previousArtworkUrl = artworkUrl;
            if (trackChanged)
            {
                lastArtworkUrl = null;
            }
            UpdateArtwork(artworkUrl);
        }
    }

    private void UpdatePlaybackStateFromState(PlaybackState state)
    {
        var duration = state.Duration;
        currentDuration = duration;
        EndTime = $"{duration.Minutes:00}:{duration.Seconds:00}";

        ErrorMessage = state.ErrorMessage ?? "";

        var isPlaying = state.Status == PlayStatus.Playing;
        PlayVisible = !isPlaying;
        PauseVisible = isPlaying;

        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(PreparationProgress));
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(AreControlsEnabled));
    }

    public void Receive(PlaybackPositionChangedMessage message)
    {
        if (ShouldIgnorePositionUpdate(message))
        {
            return;
        }

        MainThread.BeginInvokeOnMainThread(() =>
        {
            UpdatePositionFromMessage(message);
            OnPropertyChanged(nameof(ProgressText));
        });
    }

    private bool ShouldIgnorePositionUpdate(PlaybackPositionChangedMessage message)
    {
        if (!isUserInteracting)
        {
            return false;
        }

        if (message.CurrentPosition.HasValue && targetSeekProgress.HasValue && currentDuration.TotalSeconds > 0)
        {
            var actualProgress = message.CurrentPosition.Value.TotalSeconds / currentDuration.TotalSeconds;
            var progressDiff = Math.Abs(actualProgress - targetSeekProgress.Value);

            if (progressDiff < 0.02)
            {
                isUserInteracting = false;
                targetSeekProgress = null;
                return false;
            }
        }

        return true;
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

    public void Receive(PlaybackPreparationProgressMessage message)
    {
        // Handle high-frequency preparation progress updates via messaging
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
        // Avoid unnecessary updates if URL hasn't changed
        if (lastArtworkUrl == artworkUrl)
        {
            return;
        }

        var previousUrl = lastArtworkUrl;
        lastArtworkUrl = artworkUrl;

        if (string.IsNullOrEmpty(artworkUrl))
        {
            // Only clear if we currently have artwork displayed
            if (artworkSource != null)
            {
                ClearArtwork();
            }
            return;
        }

        // Only clear existing artwork if we're switching to a different artwork
        // Don't clear if we're loading artwork for the first time (to avoid showing bell icon briefly)
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
        lastArtworkUrl = null; // Reset so we can reload if needed
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

