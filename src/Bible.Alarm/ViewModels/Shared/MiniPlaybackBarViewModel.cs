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
    private readonly ILogger logger;
    private readonly IPlaybackService playbackService;
    private readonly IState<PlaybackState> playbackState;
    private bool isDisposed;

    public MiniPlaybackBarViewModel(
        ILogger logger,
        IPlaybackService playbackService,
        IState<PlaybackState> playbackState)
    {
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

    public bool PlayVisible => !IsPlaying;
    public bool PauseVisible => IsPlaying;

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
        IsPlaying = state.Status == PlayStatus.Playing;
        AreControlsEnabled = state.Status is PlayStatus.Playing or PlayStatus.Paused;

        if (state.Duration > TimeSpan.Zero)
        {
            currentDuration = state.Duration;
        }

        if (!string.IsNullOrEmpty(state.ArtworkUrl))
        {
            try
            {
                ArtworkSource = ImageSource.FromUri(new Uri(state.ArtworkUrl));
            }
            catch
            {
                ArtworkSource = null;
            }
        }
        else
        {
            ArtworkSource = null;
        }
    }

    private async Task OnStopAsync()
    {
        try
        {
            await playbackService.StopAsync();
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "MiniPlaybackBar: Error stopping playback");
        }
    }

    private async Task OnPreviousAsync()
    {
        try
        {
            Progress = 0;
            await playbackService.PlayPreviousAsync();
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "MiniPlaybackBar: Error playing previous");
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
            Progress = 0;
            await playbackService.PlayNextAsync();
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "MiniPlaybackBar: Error playing next");
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
