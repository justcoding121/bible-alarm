#nullable enable
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Stores.Actions.Playback;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;
using Serilog;
using System.IO;
#if IOS
using AVFoundation;
using Foundation;
using Bible.Alarm.Platforms.iOS.Helpers;
#endif

namespace Bible.Alarm.Services.Media;

public class AudioPlayer : IAudioPlayer
{
    private readonly ILogger _logger;
    private readonly MediaElement _mediaElement;
    private readonly IDisplayMetadataService _displayMetadataService;
    private readonly IDispatcher _dispatcher;
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

    public AudioPlayer(ILogger logger, INavigationService navigationService, IDisplayMetadataService displayMetadataService, IDispatcher dispatcher)
    {
        _logger = logger;
        _mediaElement = navigationService.GetMediaElement();
        _displayMetadataService = displayMetadataService;
        _dispatcher = dispatcher;

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

        await SafeStopMediaElementAsync(clearSource: false);

        _currentTrack = track;
        Status = PlayStatus.Loading;
        _mediaOpenedCompletionSource = new TaskCompletionSource<bool>();

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            // Clear previous artwork to prevent iOS from trying to load null data
            _mediaElement.MetadataArtworkUrl = null!;
            
#if IOS
            var processedUri = ProcessiOSUri(track.Uri);
            _mediaElement.Source = processedUri;
            _mediaElement.Volume = 1.0;
            _logger.Debug("Set MediaElement Volume to 1.0 on iOS. Source: {Source}, CurrentState: {State}", 
                _mediaElement.Source?.ToString() ?? "null",
                _mediaElement.CurrentState);
#else
            _mediaElement.Source = track.Uri;
#endif
        });

        // Wait for media to open (with timeout)
        var timeoutTask = Task.Delay(5000); // 5 second timeout
        var completedTask = await Task.WhenAny(_mediaOpenedCompletionSource.Task, timeoutTask);
        
        if (completedTask == timeoutTask)
        {
            _logger.Warning("Timeout waiting for media to open");
        }
    }

#if IOS
    /// <summary>
    /// Processes a URI for iOS MediaElement by normalizing paths and converting to file:// format.
    /// Note: MediaCacheService always returns cached file paths, never HTTP/HTTPS URLs.
    /// </summary>
    private string ProcessiOSUri(string uri)
    {
        _logger.Debug("Original track URI: {Uri}", uri);
        
        if (!uri.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessiOSFilePath(uri);
        }
        else
        {
            return ProcessiOSFileUri(uri);
        }
    }

    /// <summary>
    /// Processes a plain file path (not file:// URI) for iOS by normalizing and converting to file:// format.
    /// </summary>
    private string ProcessiOSFilePath(string filePath)
    {
        try
        {
            var normalizedPath = NormalizeFilePath(filePath);
            _logger.Debug("Normalized path: {Path} (original: {Original})", normalizedPath, filePath);
            
            // Verify file exists
            if (!System.IO.File.Exists(normalizedPath))
            {
                _logger.Error("File does not exist at normalized path: {Path}", normalizedPath);
                // Try the original path as fallback
                if (System.IO.File.Exists(filePath))
                {
                    _logger.Warning("File exists at original path, using original: {Path}", filePath);
                    return ConvertToFileUri(filePath);
                }
                else
                {
                    _logger.Error("File does not exist at original path either: {Path}", filePath);
                    throw new FileNotFoundException($"File not found: {normalizedPath}");
                }
            }
            
            return ConvertToFileUri(normalizedPath);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error normalizing file path: {Path}", filePath);
            // Fallback: use original path if normalization fails
            if (System.IO.File.Exists(filePath))
            {
                _logger.Warning("Using original path as fallback: {Path}", filePath);
                return ConvertToFileUri(filePath);
            }
            else
            {
                _logger.Error("Original path also does not exist: {Path}", filePath);
                throw;
            }
        }
    }

    /// <summary>
    /// Normalizes a file path by resolving ../ and ./ segments.
    /// </summary>
    private string NormalizeFilePath(string path)
    {
        var isAbsolute = path.StartsWith("/");
        var pathParts = path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        var normalizedParts = new List<string>();
        
        foreach (var part in pathParts)
        {
            if (part == "..")
            {
                if (normalizedParts.Count > 0)
                {
                    normalizedParts.RemoveAt(normalizedParts.Count - 1);
                }
            }
            else if (part != "." && !string.IsNullOrEmpty(part))
            {
                normalizedParts.Add(part);
            }
        }
        
        return isAbsolute ? "/" + string.Join("/", normalizedParts) : string.Join("/", normalizedParts);
    }

    /// <summary>
    /// Converts a file path to a file:// URI using NSUrl for iOS.
    /// </summary>
    private string ConvertToFileUri(string filePath)
    {
        var nsUrl = NSUrl.FromFilename(filePath);
        var uri = nsUrl.AbsoluteString ?? filePath;
        _logger.Debug("Converted path to file:// URI for iOS: {Uri} (original path: {Path})", uri, filePath);
        return uri;
    }

    /// <summary>
    /// Processes a file:// URI for iOS by ensuring proper format and verifying file exists.
    /// </summary>
    private string ProcessiOSFileUri(string uri)
    {
        // Ensure proper file:// URL format for iOS (file:/// for absolute paths)
        if (!uri.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
        {
            uri = uri.Replace("file://", "file:///");
            _logger.Debug("Formatted file URI for iOS: {Uri}", uri);
        }
        
        // Verify file exists by converting to local path
        try
        {
            var fileUri = new Uri(uri);
            var localPath = fileUri.LocalPath;
            if (!System.IO.File.Exists(localPath))
            {
                _logger.Error("File does not exist at path: {Path}", localPath);
                throw new FileNotFoundException($"File not found: {localPath}");
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error verifying file:// URI: {Uri}", uri);
            throw;
        }
        
        return uri;
    }
#endif

    public async Task PlayAsync()
    {
#if IOS
        await ConfigureiOSAudioSessionBeforePlayAsync();
        var currentState = await GetCurrentMediaElementStateAsync();
        await HandlePausedStateOniOSAsync(currentState);
#endif
        
        await InvokePlayOnMainThreadAsync();
        
        // Wait briefly and check if playback started
        await Task.Delay(100);
        
#if IOS
        var stateAfterPlay = await GetCurrentMediaElementStateAsync();
        if (stateAfterPlay == MediaElementState.Playing || stateAfterPlay == MediaElementState.Buffering)
        {
            _logger.Debug("MediaElement is in {State} state after Play()", stateAfterPlay);
        }
        await RetryPlayIfNeededOniOSAsync(stateAfterPlay);
#else
        var stateAfterPlay = await MainThread.InvokeOnMainThreadAsync(() => _mediaElement.CurrentState);
        _logger.Debug("After Play() call, MediaElement state: {State}", stateAfterPlay);
#endif
    }

    /// <summary>
    /// Invokes Play() on the main thread and sets volume on iOS.
    /// </summary>
    private async Task InvokePlayOnMainThreadAsync()
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            _logger.Debug("About to call MediaElement.Play(). Current state: {State}, Source: {Source}", 
                _mediaElement.CurrentState, 
                _mediaElement.Source?.ToString() ?? "null");
            
            _mediaElement.Play();
            
#if IOS
            // Ensure volume is set to 1.0 before playing on iOS
            _mediaElement.Volume = 1.0;
            _logger.Debug("Set MediaElement Volume to 1.0 before Play() on iOS. Current Volume: {Volume}, State after Play(): {State}", 
                _mediaElement.Volume, 
                _mediaElement.CurrentState);
#endif
        });
    }

#if IOS
    /// <summary>
    /// Configures iOS audio session before playing. Critical for MediaElement to actually play audio on iOS.
    /// </summary>
    private async Task ConfigureiOSAudioSessionBeforePlayAsync()
    {
        _logger.Debug("Configuring iOS audio session before Play()");
        await ConfigureiOSAudioSessionAsync();
        _logger.Debug("iOS audio session configuration completed");
    }

    /// <summary>
    /// Gets the current MediaElement state on the main thread.
    /// </summary>
    private async Task<MediaElementState> GetCurrentMediaElementStateAsync()
    {
        return await MainThread.InvokeOnMainThreadAsync(() =>
        {
            var state = _mediaElement.CurrentState;
            _logger.Debug("MediaElement state: {CurrentState}, Source: {Source}", 
                state, 
                _mediaElement.Source?.ToString() ?? "null");
            return state;
        });
    }

    /// <summary>
    /// Handles the Paused state on iOS by stopping first, as calling Play() directly may not work.
    /// </summary>
    private async Task HandlePausedStateOniOSAsync(MediaElementState currentState)
    {
        if (currentState == MediaElementState.Paused)
        {
            _logger.Debug("MediaElement is in Paused state on iOS, stopping first then playing");
            await MainThread.InvokeOnMainThreadAsync(() => _mediaElement.Stop());
            await Task.Delay(50);
            
            var stateAfterStop = await GetCurrentMediaElementStateAsync();
            _logger.Debug("MediaElement state after Stop(): {State}", stateAfterStop);
        }
    }

    /// <summary>
    /// Retries Play() on iOS if playback didn't start after initial attempt.
    /// </summary>
    private async Task RetryPlayIfNeededOniOSAsync(MediaElementState stateAfterPlay)
    {
        if (stateAfterPlay != MediaElementState.Playing && stateAfterPlay != MediaElementState.Buffering)
        {
            _logger.Debug("MediaElement not in Playing/Buffering state after Play(), waiting longer and retrying. Current state: {State}", stateAfterPlay);
            await Task.Delay(200);
            
            var stateAfterWait = await GetCurrentMediaElementStateAsync();
            if (stateAfterWait != MediaElementState.Playing && stateAfterWait != MediaElementState.Buffering)
            {
                _logger.Debug("MediaElement still not playing after wait, attempting Play() again. State: {State}", stateAfterWait);
                await MainThread.InvokeOnMainThreadAsync(() => _mediaElement.Play());
                await Task.Delay(100);
                stateAfterWait = await GetCurrentMediaElementStateAsync();
                _logger.Debug("After retry, MediaElement state: {State}", stateAfterWait);
            }
        }
    }
#endif

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
        // Dispatch Fluxor action
        _dispatcher.Dispatch(new PlaybackMetadataChangedAction
        {
            Title = meta.Title,
            Artist = meta.Artist,
            Album = meta.Album,
            ArtworkUrl = meta.ArtworkUrl
        });
    }

    private void SendPositionUpdate()
    {
        // Dispatch Fluxor action
        _dispatcher.Dispatch(new PlaybackPositionChangedAction
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
            _logger.Debug("Status changed to Playing, starting position timer. CurrentState: {CurrentState}", e.NewState);
            _positionTimer?.Start();
        }
        else
        {
            _logger.Debug("Status changed to {Status}, stopping position timer. CurrentState: {CurrentState}", Status, e.NewState);
            _positionTimer?.Stop();
        }
    }

    private void OnPositionChanged(object? sender, EventArgs e)
    {
        var position = CurrentPosition;
        var duration = Duration;
        var actualPosition = _mediaElement.Position;
        var actualDuration = _mediaElement.Duration;
        _logger.Debug("MediaElement position changed. Position: {Position}, Duration: {Duration}, Status: {Status}, ActualPosition: {ActualPosition}, ActualDuration: {ActualDuration}", 
            position?.ToString() ?? "null", 
            duration.ToString(), 
            Status,
            actualPosition.ToString(),
            actualDuration.ToString());
        SendPositionUpdate();
    }

    private void OnPositionTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        // Check position on main thread since MediaElement properties must be accessed on UI thread
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var position = CurrentPosition;
            var duration = Duration;
            var actualPosition = _mediaElement.Position;
            var actualDuration = _mediaElement.Duration;
            var currentState = _mediaElement.CurrentState;
            _logger.Debug("Position timer elapsed. Position: {Position}, Duration: {Duration}, Status: {Status}, ActualPosition: {ActualPosition}, ActualDuration: {ActualDuration}, CurrentState: {CurrentState}", 
                position?.ToString() ?? "null", 
                duration.ToString(), 
                Status,
                actualPosition.ToString(),
                actualDuration.ToString(),
                currentState);
            SendPositionUpdate();
        });
    }

    private void SendStatusMessage()
    {
        // Dispatch status change to Fluxor state
        _dispatcher.Dispatch(new PlaybackStatusChangedAction(Status));
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

    public async Task ResetAsync()
    {
        _isResetting = true;
        
        try
        {
            // Stop and clear source
            await SafeStopMediaElementAsync(clearSource: true);
            
            // Wait a bit for the stop to take effect
            await Task.Delay(100);
            
            // Force stop again if still in a valid state to stop
            var actualState = await MainThread.InvokeOnMainThreadAsync(() => _mediaElement.CurrentState);
            if (actualState == MediaElementState.Playing || 
                actualState == MediaElementState.Paused || 
                actualState == MediaElementState.Buffering)
            {
                _logger.Debug("MediaElement still in {State} state after first stop, forcing stop again", actualState);
                await SafeStopMediaElementAsync(clearSource: true);
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

    /// <summary>
    /// Safely stops the MediaElement by checking its state first.
    /// On Android, calling Stop() when in IDLE or ERROR states causes errors.
    /// </summary>
    /// <param name="clearSource">If true, clears the Source property after stopping. If false, only clears Source for invalid states.</param>
    private Task SafeStopMediaElementAsync(bool clearSource = true)
    {
        return MainThread.InvokeOnMainThreadAsync(() =>
        {
            // Only call Stop() if MediaElement is in a valid state (Playing, Paused, or Buffering)
            // On Android, calling Stop() when in IDLE or ERROR states causes errors
            var currentState = _mediaElement.CurrentState;
            if (currentState == MediaElementState.Playing || 
                currentState == MediaElementState.Paused || 
                currentState == MediaElementState.Buffering)
            {
                _mediaElement.Stop();
            }
            
            // Clear source if requested, or if in an invalid state (like Failed)
            if (clearSource || (currentState != MediaElementState.Stopped && currentState != MediaElementState.None))
            {
                _mediaElement.Source = null;
            }
        });
    }

#if IOS
    private async Task ConfigureiOSAudioSessionAsync()
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            iOSAudioSessionHelper.ConfigureAudioSession(_logger, "main playback");
        });
    }
#endif

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
