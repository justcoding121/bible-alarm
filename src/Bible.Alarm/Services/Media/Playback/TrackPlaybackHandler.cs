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
        bool startFromBeginning,
        int? currentScheduleId,
        Func<bool> isPreparingOrPlaying,
        Func<List<AudioPlayerTrack>?> getPlaylist,
        Action<bool> setIsPreparingTrack,
        HashSet<string> playedBibleTrackKeys)
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
            // Sync metadata (including artwork) before opening the file for playback.
            // When the track is cached, the same file is used for playback and for TagLib artwork extraction.
            // If we extract after PrepareAsync, the media player may have the file open and extraction can fail
            // on some platforms (first track shows spinner; dismiss and play again then works). Doing it first
            // ensures artwork is in state before the modal displays and avoids file contention.
            await audioPlayer.SyncMetadataForTrackAsync(track);

            await audioPlayer.PrepareAsync(track);

            // On Android with queue (SetSourceWithDummyQueue), MediaOpened may not fire when changing tracks.
            // We already synced metadata above; HandleMediaOpenedAsync will run when MediaOpened fires and can
            // refresh if needed (e.g. duration). No need to call SyncMetadataForTrackAsync again here.

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

        // Create unique key for this Bible track to track if it's been played before
        string? bibleTrackKey = null;
        var isBibleTrack = track.PlayItem?.Metadata != null && track.PlayItem.Metadata.PlayType == PlayType.Bible;
        var isFirstEncounter = false;

        if (isBibleTrack && track.PlayItem?.Metadata != null)
        {
            var trackMetadata = track.PlayItem.Metadata;
            bibleTrackKey = $"{trackMetadata.ScheduleId}:{trackMetadata.LanguageCode}:{trackMetadata.PublicationCode}:{trackMetadata.SectionCode ?? "null"}:{trackMetadata.TrackCode}";
            isFirstEncounter = !playedBibleTrackKeys.Contains(bibleTrackKey);
        }

        logger.Debug("[Resume] Checking resume conditions - startFromBeginning: {StartFromBeginning}, HasMetadata: {HasMetadata}, PlayType: {PlayType}, FinishedDuration: {FinishedDuration}, TrackIndex: {TrackIndex}, IsFirstEncounter: {IsFirstEncounter}, TrackKey: {TrackKey}",
            startFromBeginning,
            track.PlayItem?.Metadata != null,
            track.PlayItem?.Metadata?.PlayType.ToString() ?? "null",
            track.PlayItem?.Metadata?.FinishedDuration.ToString() ?? "null",
            currentTrackIndex,
            isFirstEncounter,
            bibleTrackKey ?? "null");

        // Seek ONLY on first encounter of a Bible track with saved progress
        // Conditions:
        // 1. This is the FIRST encounter of this Bible track in this session (manual or automatic)
        // 2. It's a Bible track with saved progress
        // 3. Schedule allows resume (AlwaysPlayFromStart == false) - checked via ShouldResumeFromLastPositionAsync
        // Note: Works for both manual navigation and automatic transitions on first encounter
        // Subsequent encounters (manual or automatic) will start from beginning (track already marked as played)
        var shouldCheckSeek = isFirstEncounter
            && isBibleTrack
            && track.PlayItem?.Metadata != null
            && track.PlayItem.Metadata.FinishedDuration != TimeSpan.Zero;

        logger.Debug("[Resume] Seek decision - shouldCheckSeek: {ShouldCheckSeek}, Breakdown: isFirstEncounter={IsFirstEncounter}, isBibleTrack={IsBibleTrack}, HasMetadata={HasMetadata}, HasFinishedDuration={HasFinishedDuration}, startFromBeginning={StartFromBeginning}",
            shouldCheckSeek,
            isFirstEncounter,
            isBibleTrack,
            track.PlayItem?.Metadata != null,
            track.PlayItem?.Metadata?.FinishedDuration != TimeSpan.Zero,
            startFromBeginning);

        if (shouldCheckSeek)
        {
            var shouldResume = await trackPreparationHandler.ShouldResumeFromLastPositionAsync(currentScheduleId);
            logger.Debug("[Resume] ShouldResume check result: {ShouldResume}, ScheduleId: {ScheduleId}, IsFirstEncounter: {IsFirstEncounter}, StartFromBeginning: {StartFromBeginning}",
                shouldResume, currentScheduleId, isFirstEncounter, startFromBeginning);

            if (shouldResume && track.PlayItem?.Metadata != null)
            {
                seekPosition = track.PlayItem.Metadata.FinishedDuration;
                logger.Information("[Resume] Will seek to resume position: {Position}, TrackIndex: {TrackIndex}, PlayType: {PlayType}, IsFirstEncounter: {IsFirstEncounter}, TrackKey: {TrackKey}",
                    seekPosition, currentTrackIndex, track.PlayItem.Metadata.PlayType, isFirstEncounter, bibleTrackKey);
            }
            else
            {
                logger.Debug("[Resume] ShouldResume returned false - schedule may have AlwaysPlayFromStart=true");
            }
        }
        else
        {
            logger.Debug("[Resume] Skipping seek - conditions not met. startFromBeginning: {StartFromBeginning}, IsFirstEncounter: {IsFirstEncounter}, HasMetadata: {HasMetadata}, PlayType: {PlayType}, FinishedDuration: {FinishedDuration}, TrackKey: {TrackKey}",
                startFromBeginning,
                isFirstEncounter,
                track.PlayItem?.Metadata != null,
                track.PlayItem?.Metadata?.PlayType.ToString() ?? "null",
                track.PlayItem?.Metadata?.FinishedDuration.ToString() ?? "null",
                bibleTrackKey ?? "null");
        }

        // Mark this Bible track as played (after checking seek conditions, before actually playing)
        if (isBibleTrack && bibleTrackKey != null)
        {
            playedBibleTrackKeys.Add(bibleTrackKey);
            logger.Debug("[Resume] Marked Bible track as played: TrackKey: {TrackKey}", bibleTrackKey);
        }

        // Validate seek position against track duration to prevent seeking past the end.
        // Seeking past the end causes ExoPlayer to immediately fire endedState, which triggers
        // auto-advance to the next track. This happens when FinishedDuration is stale
        // (e.g. from a previously played longer track after a publication/track change).
        if (seekPosition.HasValue)
        {
            var trackDuration = audioPlayer.Duration;
            if (trackDuration > TimeSpan.Zero && seekPosition.Value >= trackDuration)
            {
                logger.Warning("[Resume] Seek position {SeekPosition} exceeds track duration {Duration} - starting from beginning. FinishedDuration may not have been reset after a publication/track change.",
                    seekPosition.Value, trackDuration);
                seekPosition = null;
            }
            else if (trackDuration == TimeSpan.Zero && seekPosition.Value > TimeSpan.Zero)
            {
                logger.Warning("[Resume] Track duration unavailable (zero) but seek position is {SeekPosition} - starting from beginning to avoid seeking into unknown territory",
                    seekPosition.Value);
                seekPosition = null;
            }
        }

#if !IOS && !ANDROID
        // On non-iOS/Android platforms (Windows), seek BEFORE play
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

        // On iOS and Android, wait a bit for playback to actually start
        // MediaElement may need time to transition to Playing state
        // On Android, this is especially important when transitioning from music to Bible track
        // because SetSourceWithDummyQueue may need time to fully configure ExoPlayer
#if IOS || ANDROID
        await Task.Delay(300);
        var positionAfterPlay = audioPlayer.CurrentPosition;
        logger.Debug("[Playback] Position AFTER PlayAsync (after 300ms): {Position}", positionAfterPlay);

        // On iOS and Android, seek AFTER play starts - seekable ranges are more reliable once playing
        // On Android, this is critical when transitioning from music to Bible track because
        // SetSourceWithDummyQueue's player.SeekTo(currentItemIndex, 0) may interfere with seeking before play
        if (seekPosition.HasValue)
        {
            // Re-validate after play started: duration is now more accurate from stream headers
            var postPlayDuration = audioPlayer.Duration;
            if (postPlayDuration > TimeSpan.Zero && seekPosition.Value >= postPlayDuration)
            {
                logger.Warning("[Resume] Post-play validation: seek position {SeekPosition} exceeds track duration {Duration} - skipping seek",
                    seekPosition.Value, postPlayDuration);
                seekPosition = null;
            }
        }

        if (seekPosition.HasValue)
        {
            logger.Debug("[Resume] Seeking AFTER play started to position: {Position}", seekPosition.Value);
            await SeekWithRetryAsync(seekPosition.Value);

            // Verify final position
            await Task.Delay(100);
            var finalPosition = audioPlayer.CurrentPosition;
            logger.Debug("[Resume] Final position after seek: {Position}", finalPosition);
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
    /// Attempts to seek to a position with retry logic for iOS and Android.
    /// On iOS, the AVPlayer may not be fully ready even after WaitForMediaReadyAsync returns,
    /// especially when transitioning between tracks (e.g., music to Bible).
    /// On Android, ExoPlayer may need time after SetSourceWithDummyQueue and PlayAsync before seeking works reliably.
    /// This method retries the seek operation if it fails due to player not being ready.
    /// </summary>
    private async Task SeekWithRetryAsync(TimeSpan position)
    {
#if IOS || ANDROID
        // On iOS, we need to retry seeking because AVPlayer.Status may not be ReadyToPlay yet
        // even though MediaElement state is Paused. This is especially true when transitioning
        // from one source to another (e.g., music track to Bible track).
        const int maxRetries = 10;
        const int retryDelayMs = 300;

        logger.Debug("[Seek] Starting seek retry loop for position {Position}, max retries: {MaxRetries}", position, maxRetries);

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                logger.Debug("[Seek] Attempt {Attempt}: Calling SeekToAsync({Position})", attempt, position);
                await audioPlayer.SeekToAsync(position);

                // Verify the seek actually worked by checking position after a brief delay
                // On iOS/Android, the seek might silently be a no-op if seekable ranges aren't ready yet
                await Task.Delay(150);
                var currentPos = audioPlayer.CurrentPosition;

                // Check if seek actually moved the position (within 1 second tolerance)
                if (currentPos.HasValue && Math.Abs(currentPos.Value.TotalSeconds - position.TotalSeconds) < 1.0)
                {
                    logger.Debug("[Seek] Attempt {Attempt} VERIFIED SUCCESS - Target: {Target}, Current: {Current}",
                        attempt, position, currentPos);
                    return;
                }
                else
                {
                    // Seek was a no-op (probably no seekable ranges yet)
                    logger.Warning("[Seek] Attempt {Attempt} was NO-OP - Target: {Target}, Current: {Current}, will retry",
                        attempt, position, currentPos);

                    if (attempt < maxRetries)
                    {
                        await Task.Delay(retryDelayMs);
                    }
                    else
                    {
                        logger.Error("[Seek] All {MaxRetries} attempts were no-ops, will start from beginning", maxRetries);
                    }
                }
            }
            catch (InvalidOperationException ex)
            {
                logger.Warning(ex, "[Seek] Attempt {Attempt} FAILED (InvalidOperationException): {Message}",
                    attempt, ex.Message);
                if (attempt < maxRetries)
                {
                    logger.Debug("[Seek] Waiting {Delay}ms before retry...", retryDelayMs);
                    await Task.Delay(retryDelayMs);
                }
                else
                {
                    logger.Error("[Seek] All {MaxRetries} attempts FAILED, will start from beginning", maxRetries);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "[Seek] Attempt {Attempt} FAILED with unexpected error: {Message}", attempt, ex.Message);
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

