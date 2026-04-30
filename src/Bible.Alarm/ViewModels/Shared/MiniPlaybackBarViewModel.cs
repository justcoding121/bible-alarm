#nullable enable
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Stores;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Fluxor;
using Serilog;

namespace Bible.Alarm.ViewModels.Shared;

public sealed partial class MiniPlaybackBarViewModel : ObservableObject,
    IRecipient<PlaybackPositionChangedMessage>,
    IRecipient<BeginStoppingPlaybackMessage>,
    IRecipient<NextButtonPressedMessage>,
    IRecipient<PreviousButtonPressedMessage>,
    IDisposable
{
    public static MiniPlaybackBarViewModel? Instance { get; private set; }

    private readonly ILogger logger;
    private readonly IPlaybackService playbackService;
    private readonly IState<PlaybackState> playbackState;
    private bool isDisposed;
    private string? lastArtworkUrl;

    public MiniPlaybackBarViewModel(
        ILogger logger,
        IPlaybackService playbackService,
        IState<PlaybackState> playbackState)
    {
        Instance = this;
        this.logger = logger;
        this.playbackService = playbackService;
        this.playbackState = playbackState;

        StopCommand = new AsyncRelayCommand(OnStopAsync);
        PreviousCommand = new AsyncRelayCommand(OnPreviousAsync);
        PlayPauseCommand = new AsyncRelayCommand(OnPlayPauseAsync);
        NextCommand = new AsyncRelayCommand(OnNextAsync);
        MaximizeCommand = new AsyncRelayCommand(OnMaximizeAsync);

        WeakReferenceMessenger.Default.Register<PlaybackPositionChangedMessage>(this);
        WeakReferenceMessenger.Default.Register<BeginStoppingPlaybackMessage>(this);
        WeakReferenceMessenger.Default.Register<NextButtonPressedMessage>(this);
        WeakReferenceMessenger.Default.Register<PreviousButtonPressedMessage>(this);
        playbackState.StateChanged += OnPlaybackStateChanged;

        SyncFromState(playbackState.Value);
    }

    private bool isVisible;
    public bool IsVisible
    {
        get => isVisible;
        set
        {
            if (SetProperty(ref isVisible, value) && value)
            {
                isTrackChangeBusy = false;
                hasSeenTrackTransition = false;
                IsStopping = false;
                IsMaximizeBusy = false;
                IsPreviousBusy = false;
                IsNextBusy = false;
                SyncFromState(playbackState.Value);
            }
        }
    }

    private double progress;
    public double Progress
    {
        get => progress;
        set => SetProperty(ref progress, value);
    }

    private string? title;
    public string? Title
    {
        get => title;
        set => SetProperty(ref title, value);
    }

    private ImageSource? artworkSource;
    public ImageSource? ArtworkSource
    {
        get => artworkSource;
        set
        {
            if (SetProperty(ref artworkSource, value))
            {
                OnPropertyChanged(nameof(ShowArtworkFallback));
            }
        }
    }

    private bool isArtworkLoading;
    public bool IsArtworkLoading
    {
        get => isArtworkLoading;
        set
        {
            if (SetProperty(ref isArtworkLoading, value))
            {
                OnPropertyChanged(nameof(ShowArtworkFallback));
            }
        }
    }

    public bool ShowArtworkFallback => ArtworkSource == null && !IsArtworkLoading;

    private bool isPlaying;
    public bool IsPlaying
    {
        get => isPlaying;
        set
        {
            if (SetProperty(ref isPlaying, value))
            {
                OnPropertyChanged(nameof(PlayVisible));
                OnPropertyChanged(nameof(PauseVisible));
            }
        }
    }

    private bool isStopping;
    public bool IsStopping
    {
        get => isStopping;
        set => SetProperty(ref isStopping, value);
    }

    private bool isTrackChangeBusy;
    private bool hasSeenTrackTransition;

    public bool PlayVisible => !IsPlaying;
    public bool PauseVisible => IsPlaying;

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

    private bool canPlayNext;
    public bool CanPlayNext
    {
        get => canPlayNext;
        set
        {
            if (SetProperty(ref canPlayNext, value))
            {
                OnPropertyChanged(nameof(IsNextEnabled));
            }
        }
    }

    private bool canPlayPrevious;
    public bool CanPlayPrevious
    {
        get => canPlayPrevious;
        set
        {
            if (SetProperty(ref canPlayPrevious, value))
            {
                OnPropertyChanged(nameof(IsPreviousEnabled));
            }
        }
    }

    private bool areControlsEnabled;
    public bool AreControlsEnabled
    {
        get => areControlsEnabled;
        set
        {
            if (SetProperty(ref areControlsEnabled, value))
            {
                OnPropertyChanged(nameof(IsPreviousEnabled));
                OnPropertyChanged(nameof(IsNextEnabled));
            }
        }
    }

    private bool isMaximizeBusy;
    public bool IsMaximizeBusy
    {
        get => isMaximizeBusy;
        set
        {
            if (SetProperty(ref isMaximizeBusy, value))
            {
                OnPropertyChanged(nameof(ShowMaximizeButton));
            }
        }
    }

    public bool ShowMaximizeButton => !IsMaximizeBusy;

    public bool IsPreviousEnabled => AreControlsEnabled && CanPlayPrevious;
    public bool IsNextEnabled => AreControlsEnabled && CanPlayNext;

    public IAsyncRelayCommand StopCommand { get; }
    public IAsyncRelayCommand PreviousCommand { get; }
    public IAsyncRelayCommand PlayPauseCommand { get; }
    public IAsyncRelayCommand NextCommand { get; }
    public IAsyncRelayCommand MaximizeCommand { get; }

    private TimeSpan currentDuration;

    public void Receive(PlaybackPositionChangedMessage message)
    {
        if (isDisposed) return;

        if (message.Duration.HasValue && message.Duration.Value > TimeSpan.Zero)
        {
            currentDuration = message.Duration.Value;
        }

        if (message.CurrentPosition.HasValue && currentDuration > TimeSpan.Zero)
        {
            var newProgress = message.CurrentPosition.Value.TotalSeconds / currentDuration.TotalSeconds;
            Progress = Math.Clamp(newProgress, 0, 1);
        }
    }

    public void Receive(BeginStoppingPlaybackMessage message)
    {
        if (isDisposed) return;

        if (MainThread.IsMainThread)
        {
            IsStopping = true;
            AreControlsEnabled = false;
        }
        else
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                IsStopping = true;
                AreControlsEnabled = false;
            });
        }
    }

    public void Receive(NextButtonPressedMessage message)
    {
        if (isDisposed) return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            isTrackChangeBusy = true;
            hasSeenTrackTransition = false;
            IsNextBusy = true;
            AreControlsEnabled = false;
            Progress = 0;
        });
    }

    public void Receive(PreviousButtonPressedMessage message)
    {
        if (isDisposed) return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            isTrackChangeBusy = true;
            hasSeenTrackTransition = false;
            IsPreviousBusy = true;
            AreControlsEnabled = false;
            Progress = 0;
        });
    }

    private void OnPlaybackStateChanged(object? sender, EventArgs e)
    {
        if (isDisposed) return;

        try
        {
            var state = playbackState.Value;
            MainThread.BeginInvokeOnMainThread(() => SyncFromState(state));
        }
        catch (Exception ex)
        {
            logger.Debug(ex, AppConstants.Logging.MiniPlaybackBarDiagnosticsLog.ErrorSyncingFromPlaybackState);
        }
    }

    private void ClearMiniBarTrackChangeBusy()
    {
        isTrackChangeBusy = false;
        hasSeenTrackTransition = false;
        IsPreviousBusy = false;
        IsNextBusy = false;
    }

    /// <summary>
    /// Returns true when <see cref="SyncFromState"/> should return without applying the rest of the UI.
    /// </summary>
    private bool ShouldDeferSyncForTrackChange(PlaybackState state)
    {
        if (!isTrackChangeBusy)
        {
            return false;
        }

        if (!hasSeenTrackTransition)
        {
            if (state.Status != PlayStatus.Playing && state.Status != PlayStatus.Paused)
            {
                hasSeenTrackTransition = true;
            }

            return true;
        }

        var trackReady = state.Status is PlayStatus.Playing or PlayStatus.Paused;
        var terminalState = state.Status is PlayStatus.Failed or PlayStatus.Ended
                            || (state.Status == PlayStatus.Stopped
                                && !state.IsTransitioningTrack && !state.IsAutoAdvancing);

        if (trackReady || terminalState)
        {
            ClearMiniBarTrackChangeBusy();
            return false;
        }

        return true;
    }

    private void SyncFromState(PlaybackState state)
    {
        if (isDisposed) return;

        if (isStopping)
        {
            return;
        }

        if (ShouldDeferSyncForTrackChange(state))
        {
            return;
        }

        IsMaximizeBusy = false;

        Title = state.Title;
        CanPlayNext = state.CanPlayNext;
        CanPlayPrevious = state.CanPlayPrevious;

        // IsPlaying drives the play/pause button visual only — stays true during auto-advance,
        // track transitions, and transient Stopped (schedule still active) so the bar looks
        // "playing" without jarring state flips.
        IsPlaying = state.Status == PlayStatus.Playing
                    || state.IsAutoAdvancing
                    || state.IsTransitioningTrack
                    || (state.IsPreparingOrPlaying && state.Status == PlayStatus.Stopped);

        // Controls are only interactive when a track is stably playing or paused.
        // Auto-advance and track transitions disable controls until the new track is ready,
        // matching the full playback modal's behaviour.
        AreControlsEnabled = state.Status is PlayStatus.Playing or PlayStatus.Paused;

        if (state.Duration > TimeSpan.Zero)
        {
            currentDuration = state.Duration;
        }

        UpdateArtwork(state.ArtworkUrl);

        if (ArtworkSource == null)
        {
            IsArtworkLoading = state.IsTransitioningTrack || state.IsAutoAdvancing
                               || state.Status == PlayStatus.Loading;
        }
        else
        {
            IsArtworkLoading = false;
        }
    }

    private void UpdateArtwork(string? artworkUrl)
    {
        if (lastArtworkUrl == artworkUrl && ArtworkSource != null)
        {
            return;
        }

        lastArtworkUrl = artworkUrl;

        if (string.IsNullOrEmpty(artworkUrl))
        {
            ArtworkSource = null;
            return;
        }

        try
        {
            if (Uri.TryCreate(artworkUrl, UriKind.Absolute, out var uri) &&
                uri.Scheme is "http" or "https")
            {
                ArtworkSource = ImageSource.FromUri(uri);
                return;
            }

            if (artworkUrl.StartsWith(MediaUriSchemeConstants.FilePrefix, StringComparison.OrdinalIgnoreCase))
            {
                var localPath = new Uri(artworkUrl).LocalPath;
                if (File.Exists(localPath))
                {
                    ArtworkSource = ImageSource.FromFile(localPath);
                    return;
                }
            }

            if (Path.IsPathRooted(artworkUrl) && File.Exists(artworkUrl))
            {
                ArtworkSource = ImageSource.FromFile(artworkUrl);
                return;
            }

            ArtworkSource = null;
        }
        catch (Exception ex)
        {
            logger.Debug(ex, AppConstants.Logging.MiniPlaybackBarDiagnosticsLog.ErrorLoadingArtworkFromUrl, artworkUrl);
            ArtworkSource = null;
        }
    }

    private async Task OnStopAsync()
    {
        try
        {
            IsStopping = true;
            AreControlsEnabled = false;
            await Task.Delay(50);
            await playbackService.StopAsync();
        }
        catch (Exception ex)
        {
            logger.Warning(ex, AppConstants.Logging.MiniPlaybackBarDiagnosticsLog.ErrorStoppingPlayback);
            IsStopping = false;
        }
    }

    private async Task OnPreviousAsync()
    {
        try
        {
            isTrackChangeBusy = true;
            IsPreviousBusy = true;
            AreControlsEnabled = false;
            Progress = 0;
            await Task.Delay(50);
            await playbackService.PlayPreviousAsync();
        }
        catch (Exception ex)
        {
            logger.Warning(ex, AppConstants.Logging.MiniPlaybackBarDiagnosticsLog.ErrorPlayingPrevious);
            isTrackChangeBusy = false;
            hasSeenTrackTransition = false;
            IsPreviousBusy = false;
        }
    }

    private async Task OnPlayPauseAsync()
    {
        try
        {
            if (IsPlaying)
            {
                await playbackService.PauseAsync();
            }
            else
            {
                await playbackService.PlayAsync();
            }
        }
        catch (Exception ex)
        {
            logger.Warning(ex, AppConstants.Logging.MiniPlaybackBarDiagnosticsLog.ErrorTogglingPlayPause);
        }
    }

    private async Task OnNextAsync()
    {
        try
        {
            isTrackChangeBusy = true;
            IsNextBusy = true;
            AreControlsEnabled = false;
            Progress = 0;
            await Task.Delay(50);
            await playbackService.PlayNextAsync();
        }
        catch (Exception ex)
        {
            logger.Warning(ex, AppConstants.Logging.MiniPlaybackBarDiagnosticsLog.ErrorPlayingNext);
            isTrackChangeBusy = false;
            hasSeenTrackTransition = false;
            IsNextBusy = false;
        }
    }

    private async Task OnMaximizeAsync()
    {
        if (!AreControlsEnabled) return;

        try
        {
            IsMaximizeBusy = true;
            await Task.Delay(50);
            WeakReferenceMessenger.Default.Send(new MaximizePlaybackMessage());
        }
        catch (Exception ex)
        {
            logger.Warning(ex, AppConstants.Logging.MiniPlaybackBarDiagnosticsLog.ErrorMaximizingPlayback);
            IsMaximizeBusy = false;
        }
    }

    public void Dispose()
    {
        if (isDisposed) return;
        isDisposed = true;

        playbackState.StateChanged -= OnPlaybackStateChanged;
        WeakReferenceMessenger.Default.Unregister<PlaybackPositionChangedMessage>(this);
        WeakReferenceMessenger.Default.Unregister<BeginStoppingPlaybackMessage>(this);
        WeakReferenceMessenger.Default.Unregister<NextButtonPressedMessage>(this);
        WeakReferenceMessenger.Default.Unregister<PreviousButtonPressedMessage>(this);
    }
}
