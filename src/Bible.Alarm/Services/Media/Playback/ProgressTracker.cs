#nullable enable
using System.Timers;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Serilog;
using Timer = System.Timers.Timer;

namespace Bible.Alarm.Services.Media.Playback;

/// <summary>
/// Handles progress tracking and saving for Bible tracks and music tracks.
/// Separated from PlaybackService for better modularity.
/// </summary>
public sealed class ProgressTracker : IDisposable
{
    private readonly IPlaylistService playlistService;
    private readonly IAudioPlayer audioPlayer;
    private readonly ILogger logger;
    private readonly Timer progressSaveTimer;

    // Track if we've already marked the current music track as finished
    private int? lastMusicTrackIndex = null;
    private bool hasMarkedCurrentMusicTrackAsFinished = false;

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

    public void StartIfBiblePublicationTrack(List<AudioPlayerTrack>? playlist, int currentTrackIndex)
    {
        if (playlist == null || currentTrackIndex < 0 || currentTrackIndex >= playlist.Count)
        {
            return;
        }

        var track = playlist[currentTrackIndex];

        // Reset music track finished flag when track changes
        if (lastMusicTrackIndex != currentTrackIndex)
        {
            hasMarkedCurrentMusicTrackAsFinished = false;
            lastMusicTrackIndex = currentTrackIndex;
        }

        // Start timer for both Bible and Music tracks
        // For Bible tracks: saves progress periodically
        // For Music tracks: marks as finished on first progress update (only once due to hasMarkedCurrentMusicTrackAsFinished flag)
        progressSaveTimer.Start();
    }

    public void Stop()
    {
        progressSaveTimer.Stop();
        // Reset flags when stopping
        hasMarkedCurrentMusicTrackAsFinished = false;
        lastMusicTrackIndex = null;
    }

    private Func<Task>? saveProgressCallback;

    public void SetSaveProgressCallback(Func<Task> callback)
    {
        saveProgressCallback = callback;
    }

    private void OnProgressSaveTimerElapsed(object? sender, ElapsedEventArgs e) => _ = saveProgressCallback?.Invoke() ?? Task.CompletedTask;

    public async Task SaveProgressAsync(List<AudioPlayerTrack>? playlist, int currentTrackIndex, bool forceSave = false)
    {
        if (playlist == null || currentTrackIndex < 0 || currentTrackIndex >= playlist.Count)
        {
            return;
        }

        var track = playlist[currentTrackIndex];

        // Handle music tracks: mark as finished on first progress update
        if (track.PlayItem.Metadata.PlayType == PlayType.Music)
        {
            // Only mark as finished once per track
            if (!hasMarkedCurrentMusicTrackAsFinished && audioPlayer.Status == PlayStatus.Playing)
            {
                try
                {
                    var currentPosition = audioPlayer.CurrentPosition;
                    if (currentPosition.HasValue && currentPosition.Value > TimeSpan.Zero)
                    {
                        logger.Information(
                            "Marking music track as finished on first progress update - ScheduleId: {ScheduleId}, TrackCode: {TrackCode}, Position: {Position}",
                            track.PlayItem.Metadata.ScheduleId,
                            track.PlayItem.Metadata.TrackCode,
                            currentPosition.Value);

                        await playlistService.MarkTrackAsFinished(track.PlayItem.Metadata);
                        hasMarkedCurrentMusicTrackAsFinished = true;
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Error marking music track as finished on progress update");
                }
            }
            return;
        }

        // Only save progress for Bible tracks
        if (track.PlayItem.Metadata.PlayType != PlayType.Bible)
        {
            return;
        }

        // Timer-driven saves only run while actively playing.
        // Explicit saves (forceSave) also save when paused — the player status may
        // have already changed to Paused (e.g. Media3 auto-paused on BT disconnect)
        // before our PauseAsync handler runs.
        if (!forceSave && audioPlayer.Status != PlayStatus.Playing)
        {
            return;
        }

        try
        {
            var currentPosition = audioPlayer.CurrentPosition;
            if (currentPosition.HasValue && currentPosition.Value > TimeSpan.Zero)
            {
                track.PlayItem.Metadata.FinishedDuration = currentPosition.Value;
                await playlistService.MarkTrackAsPlayed(track.PlayItem.Metadata);
                if (forceSave)
                {
                    logger.Debug("Force-saved progress: ScheduleId={ScheduleId}, Position={Position}",
                        track.PlayItem.Metadata.ScheduleId, currentPosition.Value);
                }
            }
            else if (forceSave && track.PlayItem.Metadata.FinishedDuration > TimeSpan.Zero)
            {
                // Player position unavailable (e.g. resources released) but in-memory
                // metadata still holds the position from the last timer tick. Persist it.
                await playlistService.MarkTrackAsPlayed(track.PlayItem.Metadata);
                logger.Debug("Force-saved in-memory progress (player position unavailable): ScheduleId={ScheduleId}, Position={Position}",
                    track.PlayItem.Metadata.ScheduleId, track.PlayItem.Metadata.FinishedDuration);
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

