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
using TagLib;
using SystemFile = System.IO.File;

namespace Bible.Alarm.Services.Media;

public class AudioPlayer : IAudioPlayer
{
    private static readonly ObservableMessenger AudioMessenger = new();

    private readonly ILogger _logger;
    private readonly MediaElement _mediaElement;
    private System.Timers.Timer? _positionTimer;

    private AudioPlayerTrack? _currentTrack;

    public TimeSpan? CurrentPosition => _mediaElement.Position;
    public TimeSpan Duration => _mediaElement.Duration;
    public PlayStatus Status { get; private set; } = PlayStatus.Stopped;

    public event EventHandler<EventArgs>? MediaEnded;
    public event EventHandler<EventArgs>? MediaFailed;

    public AudioPlayer(ILogger logger, INavigationService navigationService)
    {
        _logger = logger;
        _mediaElement = navigationService.GetMediaElement();

        _mediaElement.StateChanged += OnStateChanged;
        _mediaElement.MediaEnded += OnMediaEnded;
        _mediaElement.MediaFailed += OnMediaFailed;
        _mediaElement.MediaOpened += OnMediaOpened;
        _mediaElement.PositionChanged += OnPositionChanged;

        _positionTimer = new System.Timers.Timer(500);
        _positionTimer.Elapsed += (_, __) => SendPositionUpdate();
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

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            _mediaElement.Source = track.Uri;
        });
    }

    public Task PlayAsync()
    {
        return MainThread.InvokeOnMainThreadAsync(() => _mediaElement.Play());
    }

    private async void OnMediaOpened(object? sender, EventArgs e)
    {
        if (_currentTrack == null) return;

        try
        {
            var metadata = await ExtractMetadataAsync(_currentTrack.Uri);
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

    private async Task<MetaData> ExtractMetadataAsync(string uri)
    {
        return await Task.Run(() =>
        {
            try
            {
                // Convert file:// URI to local path, or use URI as-is if already a file path
                string filePath = uri.StartsWith("file://", StringComparison.OrdinalIgnoreCase)
                    ? new Uri(uri).LocalPath
                    : uri;

                // Extract metadata using TagLibSharp
                using var file = TagLib.File.Create(filePath);
                var tag = file.Tag;

                var meta = new MetaData
                {
                    Title = !string.IsNullOrEmpty(tag.Title) ? tag.Title : Path.GetFileNameWithoutExtension(filePath),
                    Artist = !string.IsNullOrEmpty(tag.FirstPerformer) ? tag.FirstPerformer : 
                             !string.IsNullOrEmpty(tag.FirstAlbumArtist) ? tag.FirstAlbumArtist : 
                             "Unknown Artist",
                    Album = !string.IsNullOrEmpty(tag.Album) ? tag.Album : null
                };

                // Extract artwork if available
                if (tag.Pictures != null && tag.Pictures.Length > 0)
                {
                    var picture = tag.Pictures[0];
                    if (picture?.Data?.Data != null)
                    {
                        meta.ArtworkBytes = picture.Data.Data;
                    }
                }

                return meta;
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, $"Failed to extract metadata from {uri}");
                
                // Return fallback metadata
                return new MetaData
                {
                    Title = Path.GetFileNameWithoutExtension(uri),
                    Artist = "Unknown Artist"
                };
            }
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
                SystemFile.WriteAllBytes(artworkPath, meta.ArtworkBytes);
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
        SendStatusMessage();
    }

    public Task SeekToAsync(TimeSpan position)
    {
        return MainThread.InvokeOnMainThreadAsync(() => _mediaElement.SeekTo(position));
    }

    public void Dispose()
    {
        _positionTimer?.Stop();
        _positionTimer?.Dispose();

        _mediaElement.StateChanged -= OnStateChanged;
        _mediaElement.MediaOpened -= OnMediaOpened;
        _mediaElement.MediaEnded -= OnMediaEnded;
        _mediaElement.MediaFailed -= OnMediaFailed;
        _mediaElement.PositionChanged -= OnPositionChanged;
    }
}
