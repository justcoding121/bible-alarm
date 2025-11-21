#nullable enable
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using CommunityToolkit.Mvvm.Messaging;
using Serilog;

namespace Bible.Alarm.Services.Media;

public class PlaybackService : IPlaybackService
{
    private readonly ILogger _logger;
    private readonly IAudioPlayer _audioPlayer;
    private readonly IPreparePlaybackService _preparePlaybackService;
    private readonly IPlaylistService _playlistService;
    private readonly IFallbackAlarmSoundService _fallbackAlarmSoundService;

    private List<AudioPlayerTrack>? _playlist;
    private int _currentTrackIndex = -1;
    private int? _currentScheduleId;
    private bool _isAlarm;
    private System.Timers.Timer? _progressSaveTimer;
    private HashSet<int> _manuallyVisitedTrackIndices = new();

    public int? CurrentScheduleId => _currentScheduleId;
    
    public bool IsPreparingOrPlaying => 
        _audioPlayer.Status == PlayStatus.Loading || 
        _audioPlayer.Status == PlayStatus.Playing || 
        _audioPlayer.Status == PlayStatus.Paused;

    public bool CanPlayNext => 
        _playlist != null && 
        _currentTrackIndex >= 0 && 
        _currentTrackIndex < _playlist.Count - 1;

    public bool CanPlayPrevious => 
        _playlist != null && 
        _currentTrackIndex > 0;

    public PlaybackService(
        ILogger logger,
        IAudioPlayer audioPlayer,
        IPreparePlaybackService preparePlaybackService,
        IPlaylistService playlistService,
        IFallbackAlarmSoundService fallbackAlarmSoundService)
    {
        _logger = logger;
        _audioPlayer = audioPlayer;
        _preparePlaybackService = preparePlaybackService;
        _playlistService = playlistService;
        _fallbackAlarmSoundService = fallbackAlarmSoundService;

        _audioPlayer.MediaEnded += OnMediaEnded;
        _audioPlayer.MediaFailed += OnMediaFailed;

        _progressSaveTimer = new System.Timers.Timer(1000);
        _progressSaveTimer.Elapsed += OnProgressSaveTimerElapsed;
        _progressSaveTimer.AutoReset = true;
    }

    public async Task PrepareAndPlayAsync(int scheduleId, bool isAlarm)
    {
        if (IsPreparingOrPlaying)
        {
            _logger.Warning($"Cannot prepare and play schedule {scheduleId} - already preparing or playing schedule {_currentScheduleId}");
            return;
        }

        WeakReferenceMessenger.Default.Send(new ShowAlarmModalMessage());

        try
        {
            _currentScheduleId = scheduleId;
            _isAlarm = isAlarm;
            _playlist = await _preparePlaybackService.PrepareTracksAsync(scheduleId);

            if (_playlist == null)
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
        if (_currentTrackIndex < 0 || _playlist == null || _currentTrackIndex >= _playlist.Count)
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
        if (IsPreparingOrPlaying)
        {
            _progressSaveTimer?.Stop();
            await _audioPlayer.PauseAsync();
        }
    }

    public async Task PlayNextAsync()
    {
        if (_playlist == null || _playlist.Count == 0)
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
        if (_playlist == null || _playlist.Count == 0)
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
        WeakReferenceMessenger.Default.Send(new PlaybackNavigationChangedMessage
        {
            CanPlayNext = CanPlayNext,
            CanPlayPrevious = CanPlayPrevious
        });
    }

    public async Task SeekForwardAsync()
    {
        if (!IsPreparingOrPlaying)
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
        if (!IsPreparingOrPlaying)
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

        WeakReferenceMessenger.Default.Send(new HideAlarmModalMessage());
    }

    private async Task ResetAsync()
    {
        _progressSaveTimer?.Stop();
        await _audioPlayer.ResetAsync();
        ResetState();
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
            return;

        var track = _playlist[_currentTrackIndex];
        await _audioPlayer.PrepareAsync(track);
        
        // Seek to saved position for Bible tracks if resume is enabled
        // But always start from beginning if startFromBeginning is true (e.g., when going to previous track)
        if (!startFromBeginning 
            && track.PlayItem.Metadata.PlayType == PlayType.Bible 
            && track.PlayItem.Metadata.FinishedDuration != TimeSpan.Zero)
        {
            var shouldResume = await ShouldResumeFromLastPositionAsync();
            if (shouldResume)
            {
                await _audioPlayer.SeekToAsync(track.PlayItem.Metadata.FinishedDuration);
            }
        }
        
        await _audioPlayer.PlayAsync();
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
            
            if (_playlist != null && _currentTrackIndex < _playlist.Count - 1)
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
            _logger.Warning($"Media failed for track at index {_currentTrackIndex}");
            
            if (_playlist != null && _currentTrackIndex < _playlist.Count - 1)
            {
                _currentTrackIndex++;
                await PlayCurrentTrackAsync();
            }
            else
            {
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
        if (_isAlarm)
        {
            // For alarms, play fallback sound instead of showing error
            await PlayFallbackAlarmSoundAsync();
        }
        else
        {
            WeakReferenceMessenger.Default.Send(new HideAlarmModalMessage());
            WeakReferenceMessenger.Default.Send(new ShowToastMessage("Failed to download media. Please check your network connection."));
            await ResetAsync();
        }
    }

    private async Task PlayFallbackAlarmSoundAsync()
    {
        try
        {
            var fallbackTrack = await _fallbackAlarmSoundService.GetFallbackAlarmTrackAsync();
            if (fallbackTrack == null)
            {
                _logger.Error("Failed to get fallback alarm track");
                WeakReferenceMessenger.Default.Send(new HideAlarmModalMessage());
                WeakReferenceMessenger.Default.Send(new ShowToastMessage("Failed to download media. Please check your network connection."));
                await ResetAsync();
                return;
            }

            _playlist = new List<AudioPlayerTrack> { fallbackTrack };
            _currentTrackIndex = 0;
            await PlayCurrentTrackAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error playing fallback alarm sound");
            WeakReferenceMessenger.Default.Send(new HideAlarmModalMessage());
            WeakReferenceMessenger.Default.Send(new ShowToastMessage("Failed to download media. Please check your network connection."));
            await ResetAsync();
        }
    }

    public void Dispose()
    {
        if (_progressSaveTimer != null)
        {
            _progressSaveTimer.Elapsed -= OnProgressSaveTimerElapsed;
            _progressSaveTimer.Stop();
            _progressSaveTimer.Dispose();
        }
        _audioPlayer.Dispose();
    }
}

