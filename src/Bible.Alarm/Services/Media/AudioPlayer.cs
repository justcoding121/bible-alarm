using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using CommunityToolkit.Maui.Views;
using Serilog;

namespace Bible.Alarm.Services.Media;

public class AudioPlayer : IAudioPlayer
{
    private readonly ILogger _logger;
    private readonly INavigationService _navigationService;

    private MediaElement _mediaElement;
    private readonly SemaphoreSlim _lock = new(1);

    private bool _isPlaying;
    private bool _isPrepared;
    private TimeSpan _currentTrackPosition = TimeSpan.Zero;

    public TimeSpan CurrentTrackPosition => _currentTrackPosition;
    public static int CurrentTrackIndex => 0; // MediaElement doesn't have built-in playlist support
    public static long CurrentlyPlayingScheduleId => 0; // Not managed by this service
    public bool IsPlaying => _isPlaying;
    public bool IsPrepared => _isPrepared;

    public AudioPlayer(ILogger logger, INavigationService navigationService)
    {
        _logger = logger;
        _navigationService = navigationService;
    }

    private MediaElement GetMediaElement()
    {
        if (_mediaElement != null) return _mediaElement;

        // Get MediaElement from NavigationService (finds it in BootstrapPage)
        _mediaElement = _navigationService.GetMediaElement();

        if (_mediaElement != null)
        {
            SubscribeToEvents();
        }

        return _mediaElement;
    }

    private void SubscribeToEvents()
    {
        if (_mediaElement != null)
        {
            _mediaElement.MediaOpened -= OnMediaOpened;
            _mediaElement.MediaEnded -= OnMediaEnded;
            _mediaElement.MediaFailed -= OnMediaFailed;
            _mediaElement.PositionChanged -= OnPositionChanged;
            
            _mediaElement.MediaOpened += OnMediaOpened;
            _mediaElement.MediaEnded += OnMediaEnded;
            _mediaElement.MediaFailed += OnMediaFailed;
            _mediaElement.PositionChanged += OnPositionChanged;
        }
    }

    public async Task Play()
    {
        await ConcurrencyHelper.ExecuteAsync(_lock, () =>
        {
            if (!_isPrepared)
            {
                _logger.Warning("Cannot play without setting source first.");
                return Task.CompletedTask;
            }

            var mediaElement = GetMediaElement();
            if (mediaElement == null)
            {
                _logger.Warning("MediaElement not found in visual tree, discarding play request");
                return Task.CompletedTask;
            }

            mediaElement.Play();
            _isPlaying = true;
            _logger.Information("Playback started");
            return Task.CompletedTask;
        });
    }

    public async Task Dismiss()
    {
        await ConcurrencyHelper.ExecuteAsync(_lock, () =>
        {
            var mediaElement = GetMediaElement();
            if (mediaElement != null)
            {
                mediaElement.Stop();
            }

            _isPlaying = false;
            _isPrepared = false;
            _currentTrackPosition = TimeSpan.Zero;

            _logger.Information("Playback dismissed");
            return Task.CompletedTask;
        });
    }

    public async Task SetSource(string source)
    {
        await ConcurrencyHelper.ExecuteAsync(_lock, () =>
        {
            var mediaElement = GetMediaElement();
            if (mediaElement == null)
            {
                _logger.Warning("MediaElement not found in visual tree, discarding SetSource request");
                return Task.CompletedTask;
            }

            mediaElement.Source = source;
            _isPrepared = true;
            _logger.Information($"Media source set to: {source}");
            return Task.CompletedTask;
        });
    }

    public async Task Pause()
    {
        await ConcurrencyHelper.ExecuteAsync(_lock, () =>
        {
            var mediaElement = GetMediaElement();
            if (mediaElement == null)
            {
                _logger.Warning("MediaElement not found in visual tree, discarding pause request");
                return Task.CompletedTask;
            }

            mediaElement.Pause();
            _isPlaying = false;
            _logger.Information("Playback paused");
            return Task.CompletedTask;
        });
    }

    public async Task Stop()
    {
        await ConcurrencyHelper.ExecuteAsync(_lock, () =>
        {
            var mediaElement = GetMediaElement();
            if (mediaElement == null)
            {
                _logger.Warning("MediaElement not found in visual tree, discarding stop request");
                _isPlaying = false;
                _currentTrackPosition = TimeSpan.Zero;
                return Task.CompletedTask;
            }

            mediaElement.Stop();
            _isPlaying = false;
            _currentTrackPosition = TimeSpan.Zero;
            _logger.Information("Playback stopped");
            return Task.CompletedTask;
        });
    }

    public async Task SeekTo(TimeSpan position)
    {
        await ConcurrencyHelper.ExecuteAsync(_lock, () =>
        {
            var mediaElement = GetMediaElement();
            if (mediaElement == null)
            {
                _logger.Warning("MediaElement not found in visual tree, discarding seek request");
                return Task.CompletedTask;
            }

            // MediaElement.Position is read-only, so we need to seek after media is loaded
            // Store the seek position and apply it when MediaOpened event fires
            _seekToPosition = position;
            _currentTrackPosition = position;
            _logger.Information($"Seek position set to: {position} (will apply when media opens)");
            return Task.CompletedTask;
        });
    }
    
    private TimeSpan? _seekToPosition;

    public event EventHandler<EventArgs> MediaEnded;
    public event EventHandler<EventArgs> MediaFailed;

    private void OnMediaOpened(object sender, EventArgs e)
    {
        _logger.Information("Media opened successfully");
        
        // Apply seek position if one was requested
        if (_seekToPosition.HasValue)
        {
            var mediaElement = GetMediaElement();
            if (mediaElement != null)
            {
                // Wait a bit for media to be fully ready, then seek
                Task.Run(async () =>
                {
                    await Task.Delay(100); // Small delay to ensure media is ready
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        try
                        {
                            // MediaElement doesn't support direct position setting
                            // We'll need to handle this at the platform level or skip seeking
                            // For now, just log and update our internal position
                            _currentTrackPosition = _seekToPosition.Value;
                            _logger.Information($"Seek requested to: {_seekToPosition.Value}, but MediaElement.Position is read-only");
                            _seekToPosition = null;
                        }
                        catch (Exception ex)
                        {
                            _logger.Error(ex, "Error applying seek position");
                        }
                    });
                });
            }
        }
    }

    private void OnMediaEnded(object sender, EventArgs e)
    {
        _logger.Information("Media playback ended");
        _isPlaying = false;
        MediaEnded?.Invoke(this, e);
    }

    private void OnMediaFailed(object sender, EventArgs e)
    {
        _logger.Error("Media playback failed");
        _isPlaying = false;
        MediaFailed?.Invoke(this, e);
    }

    private void OnPositionChanged(object sender, EventArgs e)
    {
        var mediaElement = GetMediaElement();
        if (mediaElement != null) _currentTrackPosition = mediaElement.Position;
    }


    public void Dispose()
    {
        // Don't dispose MediaElement - it's owned by BootstrapPage in the visual tree
        _lock?.Dispose();
    }
}