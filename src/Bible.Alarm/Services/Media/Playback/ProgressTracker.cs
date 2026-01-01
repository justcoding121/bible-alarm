#nullable enable
using System.Timers;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Shared.Models.Enums;
using Serilog;
using Timer = System.Timers.Timer;

namespace Bible.Alarm.Services.Media.Playback;

/// <summary>
/// Handles progress tracking and saving for Bible tracks.
/// Separated from PlaybackService for better modularity.
/// </summary>
public sealed class ProgressTracker
{
    private readonly IPlaylistService playlistService;
    private readonly IAudioPlayer audioPlayer;
    private readonly ILogger logger;
    private readonly Timer progressSaveTimer;

    public ProgressTracker(
        IPlaylistService playlistService,
        IAudioPlayer audioPlayer,
        ILogger logger)
    {
        this.playlistService = playlistService;
        this.audioPlayer = audioPlayer;
        this.logger = logger;

        progressSaveTimer = new Timer(1000);
        progressSaveTimer.Elapsed += OnProgressSaveTimerElapsed;
        progressSaveTimer.AutoReset = true;
    }

    public Timer Timer => progressSaveTimer;

    public void StartIfBibleTrack(List<AudioPlayerTrack>? playlist, int currentTrackIndex)
    {
        if (playlist == null || currentTrackIndex < 0 || currentTrackIndex >= playlist.Count)
        {
            return;
        }

        var track = playlist[currentTrackIndex];
        if (track.PlayItem.Metadata.PlayType == PlayType.Bible)
        {
            progressSaveTimer.Start();
        }
        else
        {
            progressSaveTimer.Stop();
        }
    }

    public void Stop()
    {
        progressSaveTimer.Stop();
    }

    private Func<Task>? saveProgressCallback;

    public void SetSaveProgressCallback(Func<Task> callback)
    {
        saveProgressCallback = callback;
    }

    private void OnProgressSaveTimerElapsed(object? sender, ElapsedEventArgs e) => _ = saveProgressCallback?.Invoke() ?? Task.CompletedTask;

    public async Task SaveProgressAsync(List<AudioPlayerTrack>? playlist, int currentTrackIndex)
    {
        if (playlist == null || currentTrackIndex < 0 || currentTrackIndex >= playlist.Count)
        {
            return;
        }

        var track = playlist[currentTrackIndex];

        // Only save progress for Bible tracks
        if (track.PlayItem.Metadata.PlayType != PlayType.Bible)
        {
            return;
        }

        // Only save if currently playing
        if (audioPlayer.Status != PlayStatus.Playing)
        {
            return;
        }

        try
        {
            var currentPosition = audioPlayer.CurrentPosition;
            if (currentPosition.HasValue)
            {
                // Update the track metadata with current position
                track.PlayItem.Metadata.FinishedDuration = currentPosition.Value;
                await playlistService.MarkTrackAsPlayed(track.PlayItem.Metadata);
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error saving progress");
        }
    }

    public void Dispose()
    {
        if (progressSaveTimer is { } timer)
        {
            timer.Elapsed -= OnProgressSaveTimerElapsed;
            timer.Stop();
            timer.Dispose();
        }
    }
}

