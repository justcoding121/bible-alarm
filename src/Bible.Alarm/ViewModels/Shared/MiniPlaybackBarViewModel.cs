#nullable enable
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Stores;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Fluxor;
using Serilog;

namespace Bible.Alarm.ViewModels.Shared;

public sealed class MiniPlaybackBarViewModel : ObservableObject,
    IRecipient<PlaybackPositionChangedMessage>,
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
        MaximizeCommand = new RelayCommand(OnMaximize);

        WeakReferenceMessenger.Default.Register<PlaybackPositionChangedMessage>(this);
        playbackState.StateChanged += OnPlaybackStateChanged;

        SyncFromState(playbackState.Value);
    }

    private bool isVisible;
    public bool IsVisible
    {
        get => isVisible;
        set => SetProperty(ref isVisible, value);
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
        set => SetProperty(ref artworkSource, value);
    }

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
        set
        {
            if (SetProperty(ref isStopping, value))
            {
                OnPropertyChanged(nameof(ShowStopButton));
            }
        }
    }

    public bool PlayVisible => !IsPlaying;
    public bool PauseVisible => IsPlaying;
    public bool ShowStopButton => !IsStopping;

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
        set => SetProperty(ref canPlayNext, value);
    }

    private bool canPlayPrevious;
    public bool CanPlayPrevious
    {
        get => canPlayPrevious;
        set => SetProperty(ref canPlayPrevious, value);
    }

    private bool areControlsEnabled;
    public bool AreControlsEnabled
    {
        get => areControlsEnabled;
        set => SetProperty(ref areControlsEnabled, value);
    }

    public IAsyncRelayCommand StopCommand { get; }
    public IAsyncRelayCommand PreviousCommand { get; }
    public IAsyncRelayCommand PlayPauseCommand { get; }
    public IAsyncRelayCommand NextCommand { get; }
    public IRelayCommand MaximizeCommand { get; }

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
            logger.Debug(ex, "MiniPlaybackBarViewModel: Error syncing from playback state");
        }
    }

    private void SyncFromState(PlaybackState state)
    {
        if (isDisposed) return;

        Title = state.Title;
        CanPlayNext = state.CanPlayNext;
        CanPlayPrevious = state.CanPlayPrevious;

        // Keep Pause button visible during auto-advance and track transitions to avoid flicker
        IsPlaying = state.Status == PlayStatus.Playing || state.IsAutoAdvancing || state.IsTransitioningTrack;
        AreControlsEnabled = state.Status is PlayStatus.Playing or PlayStatus.Paused
                             || state.IsAutoAdvancing || state.IsTransitioningTrack;

        if (!state.IsPreparingOrPlaying)
        {
            IsStopping = false;
        }

        if (state.Duration > TimeSpan.Zero)
        {
            currentDuration = state.Duration;
        }

        UpdateArtwork(state.ArtworkUrl);
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

            if (artworkUrl.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
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
            logger.Debug(ex, "MiniPlaybackBar: Error loading artwork from {ArtworkUrl}", artworkUrl);
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
            logger.Warning(ex, "MiniPlaybackBar: Error stopping playback");
            IsStopping = false;
        }
    }

    private async Task OnPreviousAsync()
    {
        try
        {
            IsPreviousBusy = true;
            Progress = 0;
            await Task.Delay(50);
            await playbackService.PlayPreviousAsync();
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "MiniPlaybackBar: Error playing previous");
        }
        finally
        {
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
            logger.Warning(ex, "MiniPlaybackBar: Error toggling play/pause");
        }
    }

    private async Task OnNextAsync()
    {
        try
        {
            IsNextBusy = true;
            Progress = 0;
            await Task.Delay(50);
            await playbackService.PlayNextAsync();
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "MiniPlaybackBar: Error playing next");
        }
        finally
        {
            IsNextBusy = false;
        }
    }

    private void OnMaximize()
    {
        WeakReferenceMessenger.Default.Send(new MaximizePlaybackMessage());
    }

    public void Dispose()
    {
        if (isDisposed) return;
        isDisposed = true;

        playbackState.StateChanged -= OnPlaybackStateChanged;
        WeakReferenceMessenger.Default.Unregister<PlaybackPositionChangedMessage>(this);
    }
}
