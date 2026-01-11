#nullable enable
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Stores.Actions.Playback;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Services.Media.Playback;

/// <summary>
/// Handles playback operations (play, pause, seek).
/// Separated from PlaybackService for better modularity.
/// </summary>
public sealed class PlaybackOperationHandler
{
    private readonly IAudioPlayer audioPlayer;
    private readonly IDispatcher dispatcher;
    private readonly ILogger logger;
    private readonly ProgressTracker progressTracker;

    public PlaybackOperationHandler(
        IAudioPlayer audioPlayer,
        IDispatcher dispatcher,
        ILogger logger,
        ProgressTracker progressTracker)
    {
        this.audioPlayer = audioPlayer;
        this.dispatcher = dispatcher;
        this.logger = logger;
        this.progressTracker = progressTracker;
    }

    public async Task PlayAsync(
        List<AudioPlayerTrack>? playlist,
        int currentTrackIndex,
        Func<Task> playCurrentTrackAsync)
    {
        if (currentTrackIndex < 0 || playlist is null || currentTrackIndex >= playlist.Count)
        {
            return;
        }

        if (audioPlayer.Status == PlayStatus.Paused)
        {
            await audioPlayer.ResumeAsync();
            progressTracker.StartIfBiblePublicationTrack(playlist, currentTrackIndex);
        }
        else if (audioPlayer.Status is PlayStatus.Stopped or PlayStatus.Ended)
        {
            await playCurrentTrackAsync();
        }
        else
        {
            await audioPlayer.PlayAsync();
            progressTracker.StartIfBiblePublicationTrack(playlist, currentTrackIndex);
        }
    }

    public async Task PauseAsync(
        int? currentScheduleId,
        int currentTrackIndex,
        bool isPreparingOrPlaying)
    {
        if (isPreparingOrPlaying)
        {
            progressTracker.Stop();
            await audioPlayer.PauseAsync();
            // Clear auto-advancing flag when user manually pauses
            logger.Information(
                "[PlaybackService] PauseAsync: Dispatching SetAutoAdvancingAction(false) - ScheduleId={ScheduleId}, TrackIndex={TrackIndex}",
                currentScheduleId,
                currentTrackIndex);
            dispatcher.Dispatch(new SetAutoAdvancingAction(false));
        }
    }

    public async Task SeekForwardAsync(bool isPreparingOrPlaying)
    {
        if (!isPreparingOrPlaying)
        {
            return;
        }

        var currentPosition = audioPlayer.CurrentPosition;
        if (!currentPosition.HasValue)
        {
            return;
        }

        var duration = audioPlayer.Duration;

        // Don't seek if duration is not yet loaded or is zero
        if (duration <= TimeSpan.Zero)
        {
            return;
        }

        var newPosition = currentPosition.Value.Add(TimeSpan.FromSeconds(15));

        // Clamp to just before duration to avoid triggering MediaEnded
        // Seeking to exactly duration triggers MediaEnded which advances to next track
        // For seek buttons, we want to seek within the track, not skip to next
        var maxSeekPosition = duration.Subtract(TimeSpan.FromMilliseconds(100));
        if (newPosition >= maxSeekPosition)
        {
            newPosition = maxSeekPosition;
        }

        logger.Information("Seeking forward: {CurrentPosition} -> {NewPosition} (duration: {Duration})",
            currentPosition.Value, newPosition, duration);
        await audioPlayer.SeekToAsync(newPosition);
    }

    public async Task SeekBackwardAsync(bool isPreparingOrPlaying)
    {
        if (!isPreparingOrPlaying)
        {
            return;
        }

        var currentPosition = audioPlayer.CurrentPosition;
        if (!currentPosition.HasValue)
        {
            return;
        }

        var newPosition = currentPosition.Value.Subtract(TimeSpan.FromSeconds(15));

        // Clamp to zero - can't seek before the start
        if (newPosition < TimeSpan.Zero)
        {
            newPosition = TimeSpan.Zero;
        }

        logger.Information("Seeking backward: {CurrentPosition} -> {NewPosition}",
            currentPosition.Value, newPosition);
        await audioPlayer.SeekToAsync(newPosition);
    }

    public async Task SeekToAsync(TimeSpan position, bool isPreparingOrPlaying)
    {
        if (!isPreparingOrPlaying)
        {
            return;
        }

        // Clamp position to valid range (0 to duration)
        var duration = audioPlayer.Duration;
        if (duration.TotalSeconds > 0 && position > duration)
        {
            position = duration;
        }
        if (position < TimeSpan.Zero)
        {
            position = TimeSpan.Zero;
        }

        await audioPlayer.SeekToAsync(position);
    }
}

