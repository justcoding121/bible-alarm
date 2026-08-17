#nullable enable
using System.Timers;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Serilog;
using Timer = System.Timers.Timer;

namespace Bible.Alarm.Services.Media.Playback;

public sealed partial class ProgressTracker : IDisposable
{
    private readonly IPlaylistService playlistService;
    private readonly IAudioPlayer audioPlayer;
    private readonly ILogger logger;
    private readonly Timer progressSaveTimer;

    // Track if we've already marked the current music track as finished
    private int? lastMusicTrackIndex = null;
    private bool hasMarkedCurrentMusicTrackAsFinished = false;

    // Auto-pause detection: ensures one force-save when the player transitions
    // from Playing to Paused (e.g. BT disconnect, audio focus loss) without
    // PlaybackService.PauseAsync() being called.
    private bool hasAutoSavedOnPause;

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

        // Reset music track finished flag when track changes
        if (lastMusicTrackIndex != currentTrackIndex)
        {
            hasMarkedCurrentMusicTrackAsFinished = false;
            lastMusicTrackIndex = currentTrackIndex;
        }

        hasAutoSavedOnPause = false;

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
        hasAutoSavedOnPause = false;
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

        if (await TryHandleMusicProgressAsync(track))
        {
            return;
        }

        if (track.PlayItem.Metadata.PlayType != PlayType.Bible)
        {
            return;
        }

        if (!ShouldPersistProgressThisCycle(track, forceSave))
        {
            return;
        }

        await PersistBibleTrackProgressAsync(track, forceSave);
    }

    private async Task<bool> TryHandleMusicProgressAsync(AudioPlayerTrack track)
    {
        if (track.PlayItem.Metadata.PlayType != PlayType.Music)
        {
            return false;
        }

        if (!hasMarkedCurrentMusicTrackAsFinished && audioPlayer.Status == PlayStatus.Playing)
        {
            await TryMarkMusicTrackFinishedOnceAsync(track);
        }

        return true;
    }

    private async Task TryMarkMusicTrackFinishedOnceAsync(AudioPlayerTrack track)
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

    private bool ShouldPersistProgressThisCycle(AudioPlayerTrack track, bool forceSave)
    {
        if (forceSave)
        {
            hasAutoSavedOnPause = true;
            return true;
        }

        var status = audioPlayer.Status;
        if (status == PlayStatus.Playing)
        {
            hasAutoSavedOnPause = false;
            return true;
        }

        if (status == PlayStatus.Paused && !hasAutoSavedOnPause)
        {
            hasAutoSavedOnPause = true;
            logger.Information(
                "Auto-pause detected: saving progress for resume (ScheduleId={ScheduleId})",
                track.PlayItem.Metadata.ScheduleId);
            return true;
        }

        return false;
    }

    private async Task PersistBibleTrackProgressAsync(AudioPlayerTrack track, bool forceSave)
    {
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
                return;
            }

            if ((forceSave || hasAutoSavedOnPause) && track.PlayItem.Metadata.FinishedDuration > TimeSpan.Zero)
            {
                await playlistService.MarkTrackAsPlayed(track.PlayItem.Metadata);
                logger.Debug("Saved in-memory progress (player position unavailable): ScheduleId={ScheduleId}, Position={Position}",
                    track.PlayItem.Metadata.ScheduleId, track.PlayItem.Metadata.FinishedDuration);
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error saving progress");
        }
    }

    private bool isDisposed;

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;

        if (progressSaveTimer is { } timer)
        {
            timer.Elapsed -= OnProgressSaveTimerElapsed;
            timer.Stop();
            timer.Dispose();
        }
    }
}

