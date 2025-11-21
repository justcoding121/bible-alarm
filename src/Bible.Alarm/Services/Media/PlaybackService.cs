#nullable enable
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Shared.Models.Media;
using Serilog;

namespace Bible.Alarm.Services.Media;

public class PlaybackService : IPlaybackService
{
    private readonly ILogger _logger;
    private readonly IAudioPlayer _audioPlayer;
    private readonly IPreparePlaybackService _preparePlaybackService;
    private readonly IPlaylistService _playlistService;

    private List<AudioPlayerTrack>? _playlist;
    private int _currentTrackIndex = -1;
    private int? _currentScheduleId;

    public int? CurrentScheduleId => _currentScheduleId;
    
    public bool IsPreparingOrPlaying => 
        _audioPlayer.Status == PlayStatus.Loading || 
        _audioPlayer.Status == PlayStatus.Playing || 
        _audioPlayer.Status == PlayStatus.Paused;

    public PlaybackService(
        ILogger logger,
        IAudioPlayer audioPlayer,
        IPreparePlaybackService preparePlaybackService,
        IPlaylistService playlistService)
    {
        _logger = logger;
        _audioPlayer = audioPlayer;
        _preparePlaybackService = preparePlaybackService;
        _playlistService = playlistService;

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

        try
        {
            _currentScheduleId = scheduleId;
            _playlist = await _preparePlaybackService.PrepareTracksAsync(scheduleId);
            _currentTrackIndex = 0;

            if (_playlist == null || _playlist.Count == 0)
            {
                _logger.Warning($"No tracks prepared for schedule {scheduleId}");
                _currentScheduleId = null;
                return;
            }

            await PlayCurrentTrackAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, $"Error preparing and playing schedule {scheduleId}");
            _currentScheduleId = null;
            _playlist = null;
            _currentTrackIndex = -1;
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

        _currentScheduleId = null;
        _playlist = null;
        _currentTrackIndex = -1;
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
                await StopAsync();
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
}

