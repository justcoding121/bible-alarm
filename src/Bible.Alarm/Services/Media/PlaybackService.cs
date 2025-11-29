#nullable enable
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Stores.Actions.Playback;
using CommunityToolkit.Mvvm.Messaging;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;
using Serilog;
using Bible.Alarm.Common.Interfaces.UI;

namespace Bible.Alarm.Services.Media;

public class PlaybackService : IPlaybackService
{
    private readonly ILogger _logger;
    private readonly IAudioPlayer _audioPlayer;
    private readonly IPreparePlaybackService _preparePlaybackService;
    private readonly IPlaylistService _playlistService;
    private readonly IFallbackAlarmSoundService _fallbackAlarmSoundService;
    private readonly IDispatcher _dispatcher;
    private readonly INotificationService _notificationService;
#if ANDROID
    private readonly IAndroidPlayerNotificationService? _androidPlayerNotificationService;
#endif

    private List<AudioPlayerTrack>? _playlist;
    private int _currentTrackIndex = -1;
    private int? _currentScheduleId;
    private bool _isAlarm;
    private readonly System.Timers.Timer? _progressSaveTimer;
    private readonly HashSet<int> _manuallyVisitedTrackIndices = [];

    private bool IsPreparingOrPlayingInternal
    {
        get
        {
            var isActuallyPlaying = _audioPlayer.IsActuallyPlayingOrPaused;
            var status = _audioPlayer.Status;
            return isActuallyPlaying || 
                   status == PlayStatus.Loading || 
                   status == PlayStatus.Playing || 
                   status == PlayStatus.Paused;
        }
    }

    private bool CanPlayNextInternal => 
        _playlist is not null && 
        _currentTrackIndex >= 0 && 
        _currentTrackIndex < _playlist.Count - 1;

    private bool CanPlayPreviousInternal => 
        _playlist is not null && 
        _currentTrackIndex > 0;

    public PlaybackService(
        ILogger logger,
        IAudioPlayer audioPlayer,
        IPreparePlaybackService preparePlaybackService,
        IPlaylistService playlistService,
        IFallbackAlarmSoundService fallbackAlarmSoundService,
        IDispatcher dispatcher,
        INotificationService notificationService
#if ANDROID
        , IAndroidPlayerNotificationService? androidPlayerNotificationService = null
#endif
        )
    {
        _logger = logger;
        _audioPlayer = audioPlayer;
        _preparePlaybackService = preparePlaybackService;
        _playlistService = playlistService;
        _fallbackAlarmSoundService = fallbackAlarmSoundService;
        _dispatcher = dispatcher;
        _notificationService = notificationService;
#if ANDROID
        _androidPlayerNotificationService = androidPlayerNotificationService;
        
        // Subscribe to Next/Previous button press events from system controls
        if (_androidPlayerNotificationService != null)
        {
            _androidPlayerNotificationService.NextButtonPressed += OnNextButtonPressed;
            _androidPlayerNotificationService.PreviousButtonPressed += OnPreviousButtonPressed;
        }
#endif

        _audioPlayer.MediaEnded += OnMediaEnded;
        _audioPlayer.MediaFailed += OnMediaFailed;

        _progressSaveTimer = new(1000);
        _progressSaveTimer.Elapsed += OnProgressSaveTimerElapsed;
        _progressSaveTimer.AutoReset = true;
    }

    public async Task PrepareAndPlayAsync(int scheduleId, bool isAlarm)
    {
        if (IsPreparingOrPlayingInternal)
        {
            _logger.Warning("Cannot prepare and play schedule {ScheduleId} - already preparing or playing schedule {CurrentScheduleId}. Status: {Status}", 
                scheduleId, 
                _currentScheduleId, 
                _audioPlayer.Status);
            return;
        }

        // Modal visibility is now handled reactively via PlaybackState subscription in App.xaml.cs
        // No need to send ShowAlarmModalMessage here

        try
        {
            _currentScheduleId = scheduleId;
            _isAlarm = isAlarm;
            
            // Dispatch playback started action BEFORE preparing tracks so modal opens
            // This ensures the modal is visible when we show error messages or play fallback
            _dispatcher.Dispatch(new PlaybackStartedAction(scheduleId));
            
            _playlist = await _preparePlaybackService.PrepareTracksAsync(scheduleId);

            if (_playlist is null)
            {
                _logger.Warning($"Failed to prepare tracks for schedule {scheduleId}");
                await HandlePlaybackFailureAsync();
                return;
            }

            if (_playlist.Count == 0)
            {
                _logger.Warning($"No tracks prepared for schedule {scheduleId}");
                await ResetAsync();
                return;
            }

            _currentTrackIndex = 0;
            _manuallyVisitedTrackIndices.Clear();
            
            await PlayCurrentTrackAsync();
            NotifyNavigationChanged();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, $"Error preparing and playing schedule {scheduleId}");
            await ResetAsync();
            throw;
        }
    }

    public async Task PlayAsync()
    {
        if (_currentTrackIndex < 0 || _playlist is null || _currentTrackIndex >= _playlist.Count)
            return;

        if (_audioPlayer.Status == PlayStatus.Paused)
        {
            await _audioPlayer.ResumeAsync();
            StartProgressTimerIfBibleTrack();
        }
        else if (_audioPlayer.Status == PlayStatus.Stopped || _audioPlayer.Status == PlayStatus.Ended)
        {
            await PlayCurrentTrackAsync();
        }
        else
        {
            await _audioPlayer.PlayAsync();
            StartProgressTimerIfBibleTrack();
        }
    }

    public async Task PauseAsync()
    {
        if (IsPreparingOrPlayingInternal)
        {
            _progressSaveTimer?.Stop();
            await _audioPlayer.PauseAsync();
        }
    }

    public async Task PlayNextAsync()
    {
        if (_playlist is null || _playlist.Count == 0)
            return;

        if (_currentTrackIndex < _playlist.Count - 1)
        {
            _progressSaveTimer?.Stop();
            await _audioPlayer.StopAsync();
            await MarkCurrentTrackAsPlayedAsync();
            _currentTrackIndex++;
            
            // If we've already manually visited this track, start from beginning
            // Otherwise, allow resume from saved position (for Bible tracks)
            var startFromBeginning = _manuallyVisitedTrackIndices.Contains(_currentTrackIndex);
            _manuallyVisitedTrackIndices.Add(_currentTrackIndex);
            
            await PlayCurrentTrackAsync(startFromBeginning: startFromBeginning);
            NotifyNavigationChanged();
        }
    }

    public async Task PlayPreviousAsync()
    {
        if (_playlist is null || _playlist.Count == 0)
            return;

        if (_currentTrackIndex > 0)
        {
            _progressSaveTimer?.Stop();
            await _audioPlayer.StopAsync();
            await MarkCurrentTrackAsPlayedAsync();
            _currentTrackIndex--;
            
            // Previous button always starts from beginning
            _manuallyVisitedTrackIndices.Add(_currentTrackIndex);
            await PlayCurrentTrackAsync(startFromBeginning: true);
            NotifyNavigationChanged();
        }
    }

    private void NotifyNavigationChanged()
    {
        var canPlayNext = CanPlayNextInternal;
        var canPlayPrevious = CanPlayPreviousInternal;
        
        // Dispatch Fluxor action
        _dispatcher.Dispatch(new PlaybackNavigationChangedAction(canPlayNext, canPlayPrevious));
    }

#if ANDROID
    /// <summary>
    /// Handles Next button press from system media controls (notification/lockscreen).
    /// Calls PlayNextAsync() to match the alarm modal's Next button behavior.
    /// </summary>
    private async void OnNextButtonPressed(object? sender, EventArgs e)
    {
        _logger.Debug("Next button pressed from system controls - calling PlayNextAsync");
        await PlayNextAsync();
    }

    /// <summary>
    /// Handles Previous button press from system media controls (notification/lockscreen).
    /// Calls PlayPreviousAsync() to match the alarm modal's Previous button behavior.
    /// </summary>
    private async void OnPreviousButtonPressed(object? sender, EventArgs e)
    {
        _logger.Debug("Previous button pressed from system controls - calling PlayPreviousAsync");
        await PlayPreviousAsync();
    }
#endif

    public async Task SeekForwardAsync()
    {
        if (!IsPreparingOrPlayingInternal)
            return;

        var currentPosition = _audioPlayer.CurrentPosition;
        if (!currentPosition.HasValue)
            return;

        var duration = _audioPlayer.Duration;
        
        // Don't seek if duration is not yet loaded or is zero
        if (duration <= TimeSpan.Zero)
            return;

        var newPosition = currentPosition.Value.Add(TimeSpan.FromSeconds(15));
        
        // Clamp to duration - seeking to duration will trigger MediaEnded
        // which will advance to next track, which is the desired behavior
        if (newPosition >= duration)
        {
            newPosition = duration;
        }

        await _audioPlayer.SeekToAsync(newPosition);
    }

    public async Task SeekBackwardAsync()
    {
        if (!IsPreparingOrPlayingInternal)
            return;

        var currentPosition = _audioPlayer.CurrentPosition;
        if (!currentPosition.HasValue)
            return;

        var newPosition = currentPosition.Value.Subtract(TimeSpan.FromSeconds(15));
        
        // Clamp to zero - can't seek before the start
        if (newPosition < TimeSpan.Zero)
        {
            newPosition = TimeSpan.Zero;
        }

        await _audioPlayer.SeekToAsync(newPosition);
    }

    public async Task StopAsync()
    {
        await _audioPlayer.StopAsync();
        await MarkCurrentTrackAsPlayedAsync();
        
        if (_currentScheduleId.HasValue)
        {
            await _playlistService.SaveLastPlayed(_currentScheduleId.Value);
        }

        await ResetAsync();

        // Modal visibility is now handled reactively via PlaybackState subscription in App.xaml.cs
        // No need to send HideAlarmModalMessage here
    }

    private async Task ResetAsync()
    {
        _progressSaveTimer?.Stop();
        await _audioPlayer.ResetAsync();
        ResetState();
        
        // Dispatch playback stopped action
        _dispatcher.Dispatch(new PlaybackStoppedAction());
        
        // Log reset completion for debugging
        _logger.Debug("Playback reset completed. Status: {Status}, ScheduleId: {ScheduleId}", 
            _audioPlayer.Status, 
            _currentScheduleId);
    }

    private void ResetState()
    {
        _currentScheduleId = null;
        _playlist = null;
        _currentTrackIndex = -1;
        _isAlarm = false;
        _manuallyVisitedTrackIndices.Clear();
    }

    private async Task PlayCurrentTrackAsync(bool startFromBeginning = false)
    {
        if (_playlist == null || _currentTrackIndex < 0 || _currentTrackIndex >= _playlist.Count)
        {
            _logger.Warning("Cannot play track: playlist is null or track index {TrackIndex} is out of range (playlist count: {PlaylistCount})", 
                _currentTrackIndex, 
                _playlist?.Count ?? 0);
            return;
        }

        var track = _playlist[_currentTrackIndex];
        
        if (string.IsNullOrEmpty(track.Uri))
        {
            _logger.Error("Cannot play track at index {TrackIndex}: URI is null or empty. URL: {TrackUrl}", 
                _currentTrackIndex, 
                track.PlayItem?.Url ?? "Unknown");
            await HandlePlaybackFailureAsync();
            return;
        }
        
        _logger.Debug("Preparing to play track at index {TrackIndex}. URI: {TrackUri}, URL: {TrackUrl}", 
            _currentTrackIndex, 
            track.Uri, 
            track.PlayItem?.Url ?? "Unknown");
        
        await _audioPlayer.PrepareAsync(track);
        
        // On iOS, MediaElement may need a brief moment after PrepareAsync before it can play
        // Wait for the media to be in a ready state (not None or Failed)
        await WaitForMediaReadyAsync();
        
        // Seek to saved position for Bible tracks if resume is enabled
        // But always start from beginning if startFromBeginning is true (e.g., when going to previous track)
        if (!startFromBeginning 
            && track.PlayItem?.Metadata != null
            && track.PlayItem.Metadata.PlayType == PlayType.Bible 
            && track.PlayItem.Metadata.FinishedDuration != TimeSpan.Zero)
        {
            var shouldResume = await ShouldResumeFromLastPositionAsync();
            if (shouldResume)
            {
                await _audioPlayer.SeekToAsync(track.PlayItem.Metadata.FinishedDuration);
            }
        }
        
        _logger.Debug("Calling PlayAsync for track at index {TrackIndex}", _currentTrackIndex);
        await _audioPlayer.PlayAsync();
        
        // On iOS, wait a bit longer for playback to actually start
        // MediaElement may need time to transition to Playing state
#if IOS
        await Task.Delay(300);
#endif
        
        _logger.Debug("PlayAsync completed for track at index {TrackIndex}, Status: {Status}", 
            _currentTrackIndex, 
            _audioPlayer.Status);
        StartProgressTimerIfBibleTrack();
    }

    private void StartProgressTimerIfBibleTrack()
    {
        if (_playlist == null || _currentTrackIndex < 0 || _currentTrackIndex >= _playlist.Count)
            return;

        var track = _playlist[_currentTrackIndex];
        if (track.PlayItem.Metadata.PlayType == PlayType.Bible)
        {
            _progressSaveTimer?.Start();
        }
        else
        {
            _progressSaveTimer?.Stop();
        }
    }

    private async void OnMediaEnded(object? sender, EventArgs e)
    {
        try
        {
            _progressSaveTimer?.Stop();
            await MarkCurrentTrackAsFinishedAsync();
            
            if (_playlist is not null && _currentTrackIndex < _playlist.Count - 1)
            {
                _currentTrackIndex++;
                await PlayCurrentTrackAsync();
                NotifyNavigationChanged();
            }
            else
            {
                await StopAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error handling media ended event");
        }
    }

    private async void OnMediaFailed(object? sender, EventArgs e)
    {
        try
        {
            var trackUri = _playlist?[_currentTrackIndex]?.Uri ?? "Unknown";
            var trackUrl = _playlist?[_currentTrackIndex]?.PlayItem?.Url ?? "Unknown";
            
            _logger.Warning("Media failed for track at index {TrackIndex}. URI: {TrackUri}, URL: {TrackUrl}", 
                _currentTrackIndex, 
                trackUri, 
                trackUrl);
            
            if (_playlist is not null && _currentTrackIndex < _playlist.Count - 1)
            {
                _currentTrackIndex++;
                _logger.Information("Attempting to play next track at index {NextTrackIndex}", _currentTrackIndex);
                await PlayCurrentTrackAsync();
            }
            else
            {
                _logger.Warning("No more tracks available or all tracks failed. Handling playback failure.");
                await HandlePlaybackFailureAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error handling media failed event");
        }
    }

    private async Task MarkCurrentTrackAsPlayedAsync()
    {
        if (_playlist == null || _currentTrackIndex < 0 || _currentTrackIndex >= _playlist.Count)
            return;

        try
        {
            var track = _playlist[_currentTrackIndex];
            await _playlistService.MarkTrackAsPlayed(track.PlayItem.Metadata);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error marking track as played");
        }
    }

    private void OnProgressSaveTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        _ = SaveProgressAsync();
    }

    private async Task SaveProgressAsync()
    {
        if (_playlist == null || _currentTrackIndex < 0 || _currentTrackIndex >= _playlist.Count)
            return;

        var track = _playlist[_currentTrackIndex];
        
        // Only save progress for Bible tracks
        if (track.PlayItem.Metadata.PlayType != PlayType.Bible)
            return;

        // Only save if currently playing
        if (_audioPlayer.Status != PlayStatus.Playing)
            return;

        try
        {
            var currentPosition = _audioPlayer.CurrentPosition;
            if (currentPosition.HasValue)
            {
                // Update the track metadata with current position
                track.PlayItem.Metadata.FinishedDuration = currentPosition.Value;
                await _playlistService.MarkTrackAsPlayed(track.PlayItem.Metadata);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error saving progress");
        }
    }

    private async Task<bool> ShouldResumeFromLastPositionAsync()
    {
        if (!_currentScheduleId.HasValue)
            return false;

        try
        {
            return await _playlistService.ShouldResumeFromLastPositionAsync(_currentScheduleId.Value);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error checking if should resume from last position");
            return false;
        }
    }

    private async Task WaitForMediaReadyAsync()
    {
        // On iOS, MediaElement may need a moment after PrepareAsync before it can play
        // This is especially important when transitioning between tracks (Stop -> Prepare -> Play)
        // Give it a small delay to ensure the MediaElement has fully transitioned states
        await Task.Delay(200);
        
        _logger.Debug("Media ready check completed, proceeding to play");
    }

    private async Task MarkCurrentTrackAsFinishedAsync()
    {
        if (_playlist == null || _currentTrackIndex < 0 || _currentTrackIndex >= _playlist.Count)
            return;

        try
        {
            var track = _playlist[_currentTrackIndex];
            await _playlistService.MarkTrackAsFinished(track.PlayItem.Metadata);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error marking track as finished");
        }
    }

    private async Task HandlePlaybackFailureAsync()
    {
        // Reset player and playlist service
        await ResetAsync();
        
        if (_isAlarm && _currentScheduleId.HasValue)
        {
            // For alarms, dispatch error message so alarm modal shows the error
            _dispatcher.Dispatch(new PlaybackErrorAction
            {
                ErrorMessage = "Media playback failed. Playing fallback alarm sound."
            });
            
            // Show alarm notification and try to play fallback sound
            try
            {
                await _notificationService.ShowNotificationAsync(_currentScheduleId.Value);
                await PlayFallbackAlarmSoundAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error showing alarm notification or playing fallback alarm sound");
                // Update error message if fallback also fails
                _dispatcher.Dispatch(new PlaybackErrorAction
                {
                    ErrorMessage = "Media playback failed. Check your internet connection."
                });
            }
        }
        else
        {
            // For non-alarms, show error message in UI
            _dispatcher.Dispatch(new PlaybackErrorAction
            {
                ErrorMessage = "Media download failed. Check your internet connection."
            });
            
            // Also show toast for immediate feedback
            WeakReferenceMessenger.Default.Send(new ShowToastMessage("Media download failed. Check your internet connection."));
        }
    }

    private async Task PlayFallbackAlarmSoundAsync()
    {
        try
        {
            var fallbackTrack = await _fallbackAlarmSoundService.GetFallbackAlarmTrackAsync();
            if (fallbackTrack is null)
            {
                _logger.Error("Failed to get fallback alarm track");
                // Even for alarms, if fallback fails, show error but keep modal open
                _dispatcher.Dispatch(new PlaybackErrorAction
                {
                    ErrorMessage = "Media download failed. Check your internet connection."
                });
                WeakReferenceMessenger.Default.Send(new ShowToastMessage("Media download failed. Check your internet connection."));
                // Don't reset - keep modal open so user can see the error
                return;
            }

            // Clear any previous error when starting fallback playback
            _dispatcher.Dispatch(new PlaybackErrorAction { ErrorMessage = null });
            
            _playlist = [fallbackTrack];
            _currentTrackIndex = 0;
            await PlayCurrentTrackAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error playing fallback alarm sound");
            // Even for alarms, if fallback fails, show error but keep modal open
            _dispatcher.Dispatch(new PlaybackErrorAction
            {
                ErrorMessage = "Media download failed. Check your internet connection."
            });
            WeakReferenceMessenger.Default.Send(new ShowToastMessage("Media download failed. Check your internet connection."));
            // Don't reset - keep modal open so user can see the error
        }
    }

    public void Dispose()
    {
        if (_progressSaveTimer is { } timer)
        {
            timer.Elapsed -= OnProgressSaveTimerElapsed;
            timer.Stop();
            timer.Dispose();
        }
        
        // Unsubscribe from AudioPlayer events
        _audioPlayer.MediaEnded -= OnMediaEnded;
        _audioPlayer.MediaFailed -= OnMediaFailed;
        
        _audioPlayer.Dispose();
    }
}

