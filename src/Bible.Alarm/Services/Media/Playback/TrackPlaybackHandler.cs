#nullable enable
using Bible.Alarm.Services.Media.Interfaces;
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

        if (track.PlayItem?.Metadata is { } meta)
        {
            logger.Information("[Playback] Playing track at index {TrackIndex}: ScheduleId={ScheduleId}, PublicationCode={PublicationCode}, SectionCode={SectionCode}, TrackCode={TrackCode}, LookUpPath={LookUpPath}",
                currentTrackIndex, meta.ScheduleId, meta.PublicationCode, meta.SectionCode ?? "(null)", meta.TrackCode, meta.LookUpPath);
        }

        // Mark that we're preparing a track to prevent race conditions in IsPreparingOrPlayingInternal
        setIsPreparingTrack(true);
        try
        {
            await audioPlayer.PrepareAsync(track, isFirstTrack, isLastTrack);

            // On Android with queue (SetSourceWithDummyQueue), MediaOpened may not fire when changing tracks,
            // so MediaSession/Android Auto keeps the previous track's title. Sync metadata now so Now Playing shows the correct track.
            await audioPlayer.SyncMetadataForTrackAsync(track);

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

        // Determine if we need to seek to a saved position
        TimeSpan? seekPosition = null;

        logger.Debug("[Resume] Checking resume conditions - startFromBeginning: {StartFromBeginning}, HasMetadata: {HasMetadata}, PlayType: {PlayType}, FinishedDuration: {FinishedDuration}",
            startFromBeginning,
            track.PlayItem?.Metadata != null,
            track.PlayItem?.Metadata?.PlayType.ToString() ?? "null",
            track.PlayItem?.Metadata?.FinishedDuration.ToString() ?? "null");

        if (!startFromBeginning
            && track.PlayItem?.Metadata != null
            && track.PlayItem.Metadata.PlayType == PlayType.Bible
            && track.PlayItem.Metadata.FinishedDuration != TimeSpan.Zero)
        {
            var shouldResume = await trackPreparationHandler.ShouldResumeFromLastPositionAsync(currentScheduleId);
            logger.Debug("[Resume] ShouldResume check result: {ShouldResume}, ScheduleId: {ScheduleId}", shouldResume, currentScheduleId);

            if (shouldResume)
            {
                seekPosition = track.PlayItem.Metadata.FinishedDuration;
                logger.Debug("[Resume] Will seek to resume position: {Position}", seekPosition);
            }
        }
        else
        {
            logger.Debug("[Resume] Skipping seek - conditions not met");
        }

#if !IOS
        // On non-iOS platforms, seek BEFORE play
        if (seekPosition.HasValue)
        {
            await SeekWithRetryAsync(seekPosition.Value);
        }
#endif

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

#if IOS
        var positionBeforePlay = audioPlayer.CurrentPosition;
        logger.Debug("[iOS Playback] Position BEFORE PlayAsync: {Position}", positionBeforePlay);
#endif

        await audioPlayer.PlayAsync();

        // On iOS, wait a bit longer for playback to actually start
        // MediaElement may need time to transition to Playing state
#if IOS
        await Task.Delay(300);
        var positionAfterPlay = audioPlayer.CurrentPosition;
        logger.Debug("[iOS Playback] Position AFTER PlayAsync (after 300ms): {Position}", positionAfterPlay);

        // On iOS, seek AFTER play starts - seekable ranges are more reliable once playing
        if (seekPosition.HasValue)
        {
            logger.Debug("[iOS Resume] Seeking AFTER play started to position: {Position}", seekPosition.Value);
            await SeekWithRetryAsync(seekPosition.Value);

            // Verify final position
            await Task.Delay(100);
            var finalPosition = audioPlayer.CurrentPosition;
            logger.Debug("[iOS Resume] Final position after seek: {Position}", finalPosition);
        }
#endif

        logger.Debug("PlayAsync completed for track at index {TrackIndex}, Status: {Status}",
            currentTrackIndex,
            audioPlayer.Status);

        progressTracker.StartIfBiblePublicationTrack(playlistBeforePlay, currentTrackIndex);

        // Don't clear auto-advancing flag here - let the reducer handle it when status stabilizes to Playing
        // This prevents rapid state changes from causing flicker

        return true;
    }

    /// <summary>
    /// Attempts to seek to a position with retry logic for iOS.
    /// On iOS, the AVPlayer may not be fully ready even after WaitForMediaReadyAsync returns,
    /// especially when transitioning between tracks (e.g., music to Bible).
    /// This method retries the seek operation if it fails due to player not being ready.
    /// </summary>
    private async Task SeekWithRetryAsync(TimeSpan position)
    {
#if IOS
        // On iOS, we need to retry seeking because AVPlayer.Status may not be ReadyToPlay yet
        // even though MediaElement state is Paused. This is especially true when transitioning
        // from one source to another (e.g., music track to Bible track).
        const int maxRetries = 10;
        const int retryDelayMs = 300;

        logger.Debug("[iOS Seek] Starting seek retry loop for position {Position}, max retries: {MaxRetries}", position, maxRetries);

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                logger.Debug("[iOS Seek] Attempt {Attempt}: Calling SeekToAsync({Position})", attempt, position);
                await audioPlayer.SeekToAsync(position);

                // Verify the seek actually worked by checking position after a brief delay
                // On iOS, the seek might silently be a no-op if seekable ranges aren't ready yet
                await Task.Delay(150);
                var currentPos = audioPlayer.CurrentPosition;

                // Check if seek actually moved the position (within 1 second tolerance)
                if (currentPos.HasValue && Math.Abs(currentPos.Value.TotalSeconds - position.TotalSeconds) < 1.0)
                {
                    logger.Debug("[iOS Seek] Attempt {Attempt} VERIFIED SUCCESS - Target: {Target}, Current: {Current}",
                        attempt, position, currentPos);
                    return;
                }
                else
                {
                    // Seek was a no-op (probably no seekable ranges yet)
                    logger.Warning("[iOS Seek] Attempt {Attempt} was NO-OP - Target: {Target}, Current: {Current}, will retry",
                        attempt, position, currentPos);

                    if (attempt < maxRetries)
                    {
                        await Task.Delay(retryDelayMs);
                    }
                    else
                    {
                        logger.Error("[iOS Seek] All {MaxRetries} attempts were no-ops, will start from beginning", maxRetries);
                    }
                }
            }
            catch (InvalidOperationException ex)
            {
                logger.Warning(ex, "[iOS Seek] Attempt {Attempt} FAILED (InvalidOperationException): {Message}",
                    attempt, ex.Message);
                if (attempt < maxRetries)
                {
                    logger.Debug("[iOS Seek] Waiting {Delay}ms before retry...", retryDelayMs);
                    await Task.Delay(retryDelayMs);
                }
                else
                {
                    logger.Error("[iOS Seek] All {MaxRetries} attempts FAILED, will start from beginning", maxRetries);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "[iOS Seek] Attempt {Attempt} FAILED with unexpected error: {Message}", attempt, ex.Message);
                return;
            }
        }
#else
        try
        {
            await audioPlayer.SeekToAsync(position);
            logger.Debug("Seek succeeded to position {Position}", position);
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
#endif
    }
}

