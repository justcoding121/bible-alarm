#nullable enable
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Services.UI.Interfaces;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;
using Serilog;

namespace Bible.Alarm.Services.Media;

public class AudioPlayer : IAudioPlayer
{
    private readonly ILogger _logger;
    private readonly MediaElement _mediaElement;

    private string _currentUri = string.Empty;

    public TimeSpan? CurrentPosition => _mediaElement.Position;
    public TimeSpan Duration => _mediaElement.Duration;
    public PlayStatus Status { get; private set; } = PlayStatus.Stopped;

    public event EventHandler<EventArgs>? MediaEnded;
    public event EventHandler<EventArgs>? MediaFailed;
    public event EventHandler<MetaData>? MetaDataParsed;

    public AudioPlayer(ILogger logger, INavigationService navigationService)
    {
        _logger = logger;
        _mediaElement = navigationService.GetMediaElement();

        _mediaElement.StateChanged += OnStateChanged;
        _mediaElement.MediaEnded += OnMediaEnded;
        _mediaElement.MediaFailed += OnMediaFailed;
        _mediaElement.MediaOpened += OnMediaOpened;
    }

    public async Task PrepareAsync(string uri)
    {
        if (string.IsNullOrEmpty(uri))
            throw new ArgumentException("URI cannot be null or empty");

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (_mediaElement.CurrentState != MediaElementState.Stopped)
            {
                _mediaElement.Stop();
            }
        });

        _currentUri = uri;
        Status = PlayStatus.Loading;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            _mediaElement.Source = uri;
        });
    }

    public Task PlayAsync()
    {
        return MainThread.InvokeOnMainThreadAsync(() => _mediaElement.Play());
    }

    private async void OnMediaOpened(object? sender, EventArgs e)
    {
        try
        {
            var metadata = await ExtractMetadataAsync(_currentUri);
            await ApplyMetadataToMediaElement(metadata);
            MetaDataParsed?.Invoke(this, metadata);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Metadata extraction failed");
            var fallbackMeta = new MetaData
            {
                Title = Path.GetFileNameWithoutExtension(_currentUri),
                Artist = "Unknown Artist"
            };
            await ApplyMetadataToMediaElement(fallbackMeta);
            MetaDataParsed?.Invoke(this, fallbackMeta);
        }
    }

    private async Task<MetaData> ExtractMetadataAsync(string uri)
    {
        await Task.Delay(100);

        var meta = new MetaData
        {
            Title = Path.GetFileNameWithoutExtension(uri),
            Artist = "Unknown Artist"
        };

        return meta;
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
                File.WriteAllBytes(artworkPath, meta.ArtworkBytes);
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
    }

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

    public Task SeekToAsync(TimeSpan position)
    {
        return MainThread.InvokeOnMainThreadAsync(() => _mediaElement.SeekTo(position));
    }

    public void Dispose()
    {
        _mediaElement.StateChanged -= OnStateChanged;
        _mediaElement.MediaOpened -= OnMediaOpened;
        _mediaElement.MediaEnded -= OnMediaEnded;
        _mediaElement.MediaFailed -= OnMediaFailed;
    }
}
