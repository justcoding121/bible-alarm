#nullable enable
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
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

    public int? CurrentScheduleId => _currentScheduleId;
    
    public bool IsPreparingOrPlaying => 
        _audioPlayer.Status == PlayStatus.Loading || 
        _audioPlayer.Status == PlayStatus.Playing || 
        _audioPlayer.Status == PlayStatus.Paused;

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
            await PlayCurrentTrackAsync();
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
        }
        else if (_audioPlayer.Status == PlayStatus.Stopped || _audioPlayer.Status == PlayStatus.Ended)
        {
            await PlayCurrentTrackAsync();
        }
        else
        {
            await _audioPlayer.PlayAsync();
        }
    }

    public async Task PauseAsync()
    {
        if (IsPreparingOrPlaying)
        {
            await _audioPlayer.PauseAsync();
        }
    }

    public async Task PlayNextAsync()
    {
        if (_playlist == null || _playlist.Count == 0)
            return;

        if (_currentTrackIndex < _playlist.Count - 1)
        {
            await MarkCurrentTrackAsPlayedAsync();
            _currentTrackIndex++;
            await PlayCurrentTrackAsync();
        }
    }

    public async Task PlayPreviousAsync()
    {
        if (_playlist == null || _playlist.Count == 0)
            return;

        if (_currentTrackIndex > 0)
        {
            await MarkCurrentTrackAsPlayedAsync();
            _currentTrackIndex--;
            await PlayCurrentTrackAsync();
        }
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
        await _audioPlayer.ResetAsync();
        ResetState();
    }

    private void ResetState()
    {
        _currentScheduleId = null;
        _playlist = null;
        _currentTrackIndex = -1;
        _isAlarm = false;
    }

    private async Task PlayCurrentTrackAsync()
    {
        if (_playlist == null || _currentTrackIndex < 0 || _currentTrackIndex >= _playlist.Count)
            return;

        var track = _playlist[_currentTrackIndex];
        await _audioPlayer.PrepareAsync(track);
        await _audioPlayer.PlayAsync();
    }

    private async void OnMediaEnded(object? sender, EventArgs e)
    {
        try
        {
            await MarkCurrentTrackAsFinishedAsync();
            
            if (_playlist != null && _currentTrackIndex < _playlist.Count - 1)
            {
                _currentTrackIndex++;
                await PlayCurrentTrackAsync();
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
}

