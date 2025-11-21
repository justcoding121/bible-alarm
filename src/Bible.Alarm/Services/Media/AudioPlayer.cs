#nullable enable
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Services.UI.Interfaces;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.Messaging;
using Serilog;
using System.IO;

namespace Bible.Alarm.Services.Media;

public class AudioPlayer : IAudioPlayer
{
    private static readonly ObservableMessenger AudioMessenger = new();

    private readonly ILogger _logger;
    private readonly MediaElement _mediaElement;
    private readonly IDisplayMetadataService _displayMetadataService;
    private System.Timers.Timer? _positionTimer;

    private AudioPlayerTrack? _currentTrack;
    private TaskCompletionSource<bool>? _mediaOpenedCompletionSource;

    public TimeSpan? CurrentPosition => _mediaElement.Position;
    public TimeSpan Duration => _mediaElement.Duration;
    public PlayStatus Status { get; private set; } = PlayStatus.Stopped;

    public event EventHandler<EventArgs>? MediaEnded;
    public event EventHandler<EventArgs>? MediaFailed;

    public AudioPlayer(ILogger logger, INavigationService navigationService, IDisplayMetadataService displayMetadataService)
    {
        _logger = logger;
        _mediaElement = navigationService.GetMediaElement();
        _displayMetadataService = displayMetadataService;

        _mediaElement.StateChanged += OnStateChanged;
        _mediaElement.MediaEnded += OnMediaEnded;
        _mediaElement.MediaFailed += OnMediaFailed;
        _mediaElement.MediaOpened += OnMediaOpened;
        _mediaElement.PositionChanged += OnPositionChanged;

        _positionTimer = new System.Timers.Timer(500);
        _positionTimer.Elapsed += OnPositionTimerElapsed;
        _positionTimer.AutoReset = true;
    }

    public async Task PrepareAsync(AudioPlayerTrack track)
    {
        if (track == null)
            throw new ArgumentNullException(nameof(track));
        if (string.IsNullOrEmpty(track.Uri))
            throw new ArgumentException("Track URI cannot be null or empty", nameof(track));

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (_mediaElement.CurrentState != MediaElementState.Stopped)
            {
                _mediaElement.Stop();
            }
        });

        _currentTrack = track;
        Status = PlayStatus.Loading;
        _mediaOpenedCompletionSource = new TaskCompletionSource<bool>();

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            _mediaElement.Source = track.Uri;
        });

        // Wait for media to open (with timeout)
        var timeoutTask = Task.Delay(5000); // 5 second timeout
        var completedTask = await Task.WhenAny(_mediaOpenedCompletionSource.Task, timeoutTask);
        
        if (completedTask == timeoutTask)
        {
            _logger.Warning("Timeout waiting for media to open");
        }
    }

    public Task PlayAsync()
    {
        return MainThread.InvokeOnMainThreadAsync(() => _mediaElement.Play());
    }

    private async void OnMediaOpened(object? sender, EventArgs e)
    {
        if (_currentTrack == null) return;

        // Signal that media is ready
        _mediaOpenedCompletionSource?.TrySetResult(true);

        try
        {
            var metadata = await _displayMetadataService.GetDisplayMetadataAsync(_currentTrack);
            await ApplyMetadataToMediaElement(metadata);
            SendMetadataMessage(metadata);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Metadata extraction failed");
            var fallbackMeta = new MetaData
            {
                Title = "Unknown Title",
                Artist = "Unknown Artist"
            };
            await ApplyMetadataToMediaElement(fallbackMeta);
            SendMetadataMessage(fallbackMeta);
        }
    }

    private void SendMetadataMessage(MetaData meta)
    {
        AudioMessenger.Send(new AudioMetadataMessage
        {
            Title = meta.Title,
            Artist = meta.Artist,
            Album = meta.Album,
            ArtworkUrl = meta.ArtworkUrl
        });
    }

    private void SendPositionUpdate()
    {
        AudioMessenger.Send(new AudioPositionMessage
        {
            CurrentPosition = CurrentPosition,
            Duration = Duration
        });
    }


    private async Task ApplyMetadataToMediaElement(MetaData meta)
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            _mediaElement.MetadataTitle = meta.Title ?? "";
            _mediaElement.MetadataArtist = meta.Artist ?? "";

            if (meta.ArtworkBytes != null && meta.ArtworkBytes.Length > 0)
            {
                var artworkPath = Path.Combine(FileSystem.CacheDirectory, "current_artwork.jpg");
                System.IO.File.WriteAllBytes(artworkPath, meta.ArtworkBytes);
                _mediaElement.MetadataArtworkUrl = artworkPath;
            }
            else if (!string.IsNullOrEmpty(meta.ArtworkUrl))
            {
                _mediaElement.MetadataArtworkUrl = meta.ArtworkUrl;
            }
        });
    }

    private void OnMediaEnded(object? sender, EventArgs e)
    {
        Status = PlayStatus.Ended;
        MediaEnded?.Invoke(this, EventArgs.Empty);
    }

    private void OnMediaFailed(object? sender, EventArgs e)
    {
        Status = PlayStatus.Failed;
        _mediaOpenedCompletionSource?.TrySetResult(false);
        MediaFailed?.Invoke(this, EventArgs.Empty);
    }

    private void OnStateChanged(object? sender, MediaStateChangedEventArgs e)
    {
        Status = e.NewState switch
        {
            MediaElementState.Playing => PlayStatus.Playing,
            MediaElementState.Paused => PlayStatus.Paused,
            MediaElementState.Stopped => PlayStatus.Stopped,
            MediaElementState.Buffering => PlayStatus.Loading,
            MediaElementState.Failed => PlayStatus.Failed,
            _ => PlayStatus.Stopped
        };

        SendStatusMessage();

        if (Status == PlayStatus.Playing)
        {
            _positionTimer?.Start();
        }
        else
        {
            _positionTimer?.Stop();
        }
    }

    private void OnPositionChanged(object? sender, EventArgs e)
    {
        SendPositionUpdate();
    }

    private void OnPositionTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        SendPositionUpdate();
    }

    private void SendStatusMessage()
    {
        AudioMessenger.Send(new AudioStatusMessage
        {
            Status = Status
        });
    }

    public static ObservableMessenger GetMessenger() => AudioMessenger;

    public Task PauseAsync()
    {
        return MainThread.InvokeOnMainThreadAsync(() => _mediaElement.Pause());
    }

    public Task ResumeAsync()
    {
        return MainThread.InvokeOnMainThreadAsync(() => _mediaElement.Play());
    }

    public Task StopAsync()
    {
        return MainThread.InvokeOnMainThreadAsync(() => _mediaElement.Stop());
    }

    public async Task ResetAsync()
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            _mediaElement.Stop();
            _mediaElement.Source = null;
        });
        
        Status = PlayStatus.Stopped;
        _currentTrack = null;
        _positionTimer?.Stop();
        _mediaOpenedCompletionSource?.TrySetCanceled();
        _mediaOpenedCompletionSource = null;
        SendStatusMessage();
    }

    public Task SeekToAsync(TimeSpan position)
    {
        return MainThread.InvokeOnMainThreadAsync(() => _mediaElement.SeekTo(position));
    }

    public void Dispose()
    {
        if (_positionTimer != null)
        {
            _positionTimer.Elapsed -= OnPositionTimerElapsed;
            _positionTimer.Stop();
            _positionTimer.Dispose();
        }

        _mediaElement.StateChanged -= OnStateChanged;
        _mediaElement.MediaOpened -= OnMediaOpened;
        _mediaElement.MediaEnded -= OnMediaEnded;
        _mediaElement.MediaFailed -= OnMediaFailed;
        _mediaElement.PositionChanged -= OnPositionChanged;
    }
}
