using CommunityToolkit.Maui.Views;
using Bible.Alarm.Services.Contracts;
using Serilog;

namespace Bible.Alarm.Services.Media;

public class MediaElementAudioService : IMediaElementAudioService
{
    private readonly ILogger _logger;

    private MediaElement _mediaElement;
    private readonly SemaphoreSlim _lock = new(1);

    private bool _isPlaying = false;
    private bool _isPrepared = false;
    private TimeSpan _currentTrackPosition = TimeSpan.Zero;

    public TimeSpan CurrentTrackPosition => _currentTrackPosition;
    public int CurrentTrackIndex => 0; // MediaElement doesn't have built-in playlist support
    public long CurrentlyPlayingScheduleId => 0; // Not managed by this service
    public bool IsPlaying => _isPlaying;
    public bool IsPrepared => _isPrepared;

    public MediaElementAudioService(ILogger logger)
    {
        _logger = logger;
        // Initialize MediaElement
        _mediaElement = new MediaElement
        {
            ShouldAutoPlay = false,
            ShouldLoopPlayback = false,
            ShouldShowPlaybackControls = true
        };

        // Subscribe to events
        _mediaElement.MediaOpened += OnMediaOpened;
        _mediaElement.MediaEnded += OnMediaEnded;
        _mediaElement.MediaFailed += OnMediaFailed;
        _mediaElement.PositionChanged += OnPositionChanged;
    }

    public async Task Play()
    {
        await _lock.WaitAsync();
        try
        {
            if (!_isPrepared) throw new InvalidOperationException("Cannot play without setting source first.");

            if (_mediaElement != null)
            {
                _mediaElement.Play();
                _isPlaying = true;
                _logger.Information("Playback started");
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task Dismiss()
    {
        await _lock.WaitAsync();
        try
        {
            if (_mediaElement != null) _mediaElement.Stop();

            _isPlaying = false;
            _isPrepared = false;
            _currentTrackPosition = TimeSpan.Zero;

            _logger.Information("Playback dismissed");
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SetSource(string source)
    {
        await _lock.WaitAsync();
        try
        {
            if (_mediaElement != null)
            {
                _mediaElement.Source = source;
                _isPrepared = true;
                _logger.Information($"Media source set to: {source}");
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task Pause()
    {
        await _lock.WaitAsync();
        try
        {
            if (_mediaElement != null)
            {
                _mediaElement.Pause();
                _isPlaying = false;
                _logger.Information("Playback paused");
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task Stop()
    {
        await _lock.WaitAsync();
        try
        {
            if (_mediaElement != null)
            {
                _mediaElement.Stop();
                _isPlaying = false;
                _currentTrackPosition = TimeSpan.Zero;
                _logger.Information("Playback stopped");
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SeekTo(TimeSpan position)
    {
        await _lock.WaitAsync();
        try
        {
            if (_mediaElement != null)
            {
                // MediaElement doesn't support direct position setting
                // This would need to be implemented differently for seeking
                _currentTrackPosition = position;
                _logger.Information($"Seeked to position: {position}");
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public event EventHandler<EventArgs> MediaEnded;
    public event EventHandler<EventArgs> MediaFailed;

    private void OnMediaOpened(object sender, EventArgs e)
    {
        _logger.Information("Media opened successfully");
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
        if (_mediaElement != null) _currentTrackPosition = _mediaElement.Position;
    }


    public void Dispose()
    {
        _mediaElement?.Dispose();
        _lock?.Dispose();
    }
}