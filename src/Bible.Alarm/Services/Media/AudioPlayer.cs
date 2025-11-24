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
    private bool _isResetting = false;

    public TimeSpan? CurrentPosition => _mediaElement.Position;
    public TimeSpan Duration => _mediaElement.Duration;
    public PlayStatus Status { get; private set; } = PlayStatus.Stopped;
    
    /// <summary>
    /// Gets the actual current state of the MediaElement, not just the cached Status
    /// This checks the MediaElement's CurrentState property directly
    /// </summary>
    public bool IsActuallyPlayingOrPaused
    {
        get
        {
            // If we're resetting, don't check the actual state (it might be in transition)
            if (_isResetting)
            {
                return false;
            }
            
            // If source is null, MediaElement is not playing anything
            if (_mediaElement.Source == null)
            {
                return false;
            }
            
            // CurrentState should be accessed on main thread, but for a simple check we'll do it directly
            // If this causes issues, we may need to make this async
            var actualState = _mediaElement.CurrentState;
            
            // Only return true if actually playing, paused, or buffering
            // If it's Stopped, None, Opening, or Failed, return false
            return actualState == MediaElementState.Playing || 
                   actualState == MediaElementState.Paused || 
                   actualState == MediaElementState.Buffering;
        }
    }

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
            // Clear previous artwork to prevent iOS from trying to load null data
            _mediaElement.MetadataArtworkUrl = null;
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
        
        var trackUri = _currentTrack?.Uri ?? "Unknown";
        _logger.Error("MediaElement failed to play track. URI: {TrackUri}, Source: {Source}", 
            trackUri, 
            _mediaElement.Source?.ToString() ?? "null");
        
        MediaFailed?.Invoke(this, EventArgs.Empty);
    }

    private void OnStateChanged(object? sender, MediaStateChangedEventArgs e)
    {
        // Don't update status if we're in the middle of resetting
        // This prevents race conditions where state changes fire after ResetAsync sets status to Stopped
        if (_isResetting && e.NewState != MediaElementState.Stopped)
        {
            _logger.Debug("Ignoring state change to {NewState} during reset", e.NewState);
            return;
        }

        // On iOS, MediaElement can fire state change events even after Source is set to null
        // If Source is null, we should ignore state changes (except Stopped/None) to prevent stale status updates
        // Also, if Source is null, force status to Stopped regardless of the state change
        if (_mediaElement.Source == null)
        {
            if (e.NewState != MediaElementState.Stopped && e.NewState != MediaElementState.None)
            {
                _logger.Debug("Ignoring state change to {NewState} because Source is null, forcing Status to Stopped", e.NewState);
                Status = PlayStatus.Stopped;
                SendStatusMessage();
                _positionTimer?.Stop();
                return;
            }
        }

        Status = e.NewState switch
        {
            MediaElementState.Playing => PlayStatus.Playing,
            MediaElementState.Paused => PlayStatus.Paused,
            MediaElementState.Stopped => PlayStatus.Stopped,
            MediaElementState.Buffering => PlayStatus.Loading,
            MediaElementState.Failed => PlayStatus.Failed,
            MediaElementState.None => PlayStatus.Stopped, // None is equivalent to Stopped
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
        _isResetting = true;
        
        try
        {
            // Force stop multiple times to ensure MediaElement actually stops
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _mediaElement.Stop();
                _mediaElement.Source = null;
            });
            
            // Wait a bit for the stop to take effect
            await Task.Delay(100);
            
            // Force stop again if still not stopped
            var actualState = await MainThread.InvokeOnMainThreadAsync(() => _mediaElement.CurrentState);
            if (actualState != MediaElementState.Stopped && actualState != MediaElementState.None)
            {
                _logger.Debug("MediaElement still in {State} state after first stop, forcing stop again", actualState);
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    _mediaElement.Stop();
                    _mediaElement.Source = null;
                });
                await Task.Delay(100);
            }
            
            // Ensure status is reset to Stopped
            Status = PlayStatus.Stopped;
            _currentTrack = null;
            _positionTimer?.Stop();
            _mediaOpenedCompletionSource?.TrySetCanceled();
            _mediaOpenedCompletionSource = null;
            SendStatusMessage();
            
            // Verify final state
            actualState = await MainThread.InvokeOnMainThreadAsync(() => _mediaElement.CurrentState);
            _logger.Debug("Reset completed. MediaElement state: {State}, Status: {Status}", actualState, Status);
        }
        finally
        {
            _isResetting = false;
        }
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
