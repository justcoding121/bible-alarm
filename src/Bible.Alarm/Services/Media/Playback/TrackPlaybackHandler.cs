#nullable enable
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Serilog;

namespace Bible.Alarm.Services.Media.Playback;

/// <summary>
/// Handles playing a single track.
/// Separated from PlaybackService for better modularity.
/// </summary>
public sealed class TrackPlaybackHandler
{
    private readonly IAudioPlayer audioPlayer;
    private readonly ILogger logger;
    private readonly TrackPreparationHandler trackPreparationHandler;
    private readonly ProgressTracker progressTracker;

    public TrackPlaybackHandler(
        IAudioPlayer audioPlayer,
        ILogger logger,
        TrackPreparationHandler trackPreparationHandler,
        ProgressTracker progressTracker)
    {
        this.audioPlayer = audioPlayer;
        this.logger = logger;
        this.trackPreparationHandler = trackPreparationHandler;
        this.progressTracker = progressTracker;
    }

    public async Task<bool> PlayTrackAsync(
        AudioPlayerTrack track,
        int currentTrackIndex,
        bool isFirstTrack,
        bool isLastTrack,
        bool startFromBeginning,
        int? currentScheduleId,
        Func<bool> isPreparingOrPlaying,
        Func<List<AudioPlayerTrack>?> getPlaylist,
        Action<bool> setIsPreparingTrack)
    {
        if (string.IsNullOrEmpty(track.Uri))
        {
            logger.Error("Cannot play track at index {TrackIndex}: URI is null or empty. URL: {TrackUrl}",
                currentTrackIndex,
                track.PlayItem?.Url ?? "Unknown");
            return false;
        }

        logger.Debug("Preparing to play track at index {TrackIndex}. URI: {TrackUri}, URL: {TrackUrl}",
            currentTrackIndex,
            track.Uri,
            track.PlayItem?.Url ?? "Unknown");

        // Mark that we're preparing a track to prevent race conditions in IsPreparingOrPlayingInternal
        setIsPreparingTrack(true);
        try
        {
            await audioPlayer.PrepareAsync(track, isFirstTrack, isLastTrack);

            // On iOS, MediaElement may need a brief moment after PrepareAsync before it can play
            // Wait for the media to be in a ready state (not None or Failed)
            // This also gives time for the state to transition from "Opening" to "Paused"
            await trackPreparationHandler.WaitForMediaReadyAsync();

            // Check if stop was called during PrepareAsync or WaitForMediaReadyAsync
            // This ensures stop works correctly in the gap between downloads and playback
            var playlist = getPlaylist();
            if (!isPreparingOrPlaying() || playlist == null || currentTrackIndex < 0 || currentTrackIndex >= playlist.Count)
            {
                logger.Information("Playback was stopped during PrepareAsync/WaitForMediaReadyAsync - aborting PlayCurrentTrackAsync");
                return false;
            }
        }
        finally
        {
            // Clear the flag after preparation is complete (whether successful or not)
            setIsPreparingTrack(false);
        }

        // Check again after WaitForMediaReadyAsync in case stop was called during the wait
        var playlistAfterWait = getPlaylist();
        if (!isPreparingOrPlaying() || playlistAfterWait == null || currentTrackIndex < 0 || currentTrackIndex >= playlistAfterWait.Count)
        {
            logger.Information("Playback was stopped during WaitForMediaReadyAsync - aborting PlayCurrentTrackAsync");
            return false;
        }

        // Seek to saved position for Bible tracks if resume is enabled
        // But always start from beginning if startFromBeginning is true (e.g., when going to previous track)
        if (!startFromBeginning
            && track.PlayItem?.Metadata != null
            && track.PlayItem.Metadata.PlayType == PlayType.Bible
            && track.PlayItem.Metadata.FinishedDuration != TimeSpan.Zero)
        {
            var shouldResume = await trackPreparationHandler.ShouldResumeFromLastPositionAsync(currentScheduleId);
            if (shouldResume)
            {
                try
                {
                    await audioPlayer.SeekToAsync(track.PlayItem.Metadata.FinishedDuration);
                }
                catch (InvalidOperationException ex)
                {
                    // Seek may fail if player isn't ready yet (seekable ranges not available)
                    // This is okay - we'll just start from the beginning instead
                    logger.Debug(ex, "Seek to resume position failed (player not ready), will start from beginning");
                }
                catch (Exception ex)
                {
                    // Catch any other exceptions during seek
                    logger.Warning(ex, "Unexpected error during seek to resume position, will start from beginning");
                }
            }
        }

        // Final check before starting playback - ensure stop wasn't called during seek/resume operations
        var playlistBeforePlay = getPlaylist();
        if (!isPreparingOrPlaying() || playlistBeforePlay == null || currentTrackIndex < 0 || currentTrackIndex >= playlistBeforePlay.Count)
        {
            logger.Information("Playback was stopped before PlayAsync - aborting PlayCurrentTrackAsync");
            return false;
        }

        logger.Debug("Calling PlayAsync for track at index {TrackIndex}", currentTrackIndex);
        // Note: isPreparingTrack is already false at this point, so IsPreparingOrPlayingInternal
        // will rely on actual MediaElement state, which should be ready by now
        await audioPlayer.PlayAsync();

        // On iOS, wait a bit longer for playback to actually start
        // MediaElement may need time to transition to Playing state
#if IOS
        await Task.Delay(300);
#endif

        logger.Debug("PlayAsync completed for track at index {TrackIndex}, Status: {Status}",
            currentTrackIndex,
            audioPlayer.Status);

        progressTracker.StartIfBibleTrack(playlistBeforePlay, currentTrackIndex);

        // Don't clear auto-advancing flag here - let the reducer handle it when status stabilizes to Playing
        // This prevents rapid state changes from causing flicker

        return true;
    }
}

