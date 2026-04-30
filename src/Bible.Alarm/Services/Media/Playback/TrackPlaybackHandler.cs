#nullable enable
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
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

    public async Task<bool> PlayTrackAsync(PlayTrackRequest request)
    {
        var track = request.Track;
        var currentTrackIndex = request.CurrentTrackIndex;
        var startFromBeginning = request.StartFromBeginning;
        var currentScheduleId = request.CurrentScheduleId;
        Func<bool> isPreparingOrPlaying = request.IsPreparingOrPlaying;
        Func<List<AudioPlayerTrack>?> getPlaylist = request.GetPlaylist;
        Action<bool> setIsPreparingTrack = request.SetIsPreparingTrack;
        var playedBibleTrackKeys = request.PlayedBibleTrackKeys;
        var cancellationToken = request.CancellationToken;

        if (string.IsNullOrEmpty(track.Uri))
        {
            logger.Error(AppConstants.Logging.TrackPlaybackHandlerDiagnosticsLog.CannotPlayTrackUriEmpty,
                currentTrackIndex,
                track.PlayItem?.Url ?? MediaTrackTitleHelper.UnknownTitle);
            return false;
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

            // Set internal status to Loading before changing the source so intermediate
            // MediaElement states (Stopped, Paused) during the source change are filtered
            // by ShouldIgnoreStateChange, preventing rapid play/pause button toggling.
            audioPlayer.NotifyTrackTransitionStarting();

            await audioPlayer.PrepareAsync(track);

            // On Android with queue (SetSourceWithDummyQueue), MediaOpened may not fire when changing tracks.
            // We already synced metadata above; HandleMediaOpenedAsync will run when MediaOpened fires and can
            // refresh if needed (e.g. duration). No need to call SyncMetadataForTrackAsync again here.

            // On iOS, MediaElement may need a brief moment after PrepareAsync before it can play
            // Wait for the media to be in a ready state (not None or Failed)
            // This also gives time for the state to transition from "Opening" to "Paused"
            await trackPreparationHandler.WaitForMediaReadyAsync(cancellationToken);

            // Check if stop was called during PrepareAsync or WaitForMediaReadyAsync
            // This ensures stop works correctly in the gap between downloads and playback
            var playlist = getPlaylist();
            if (!isPreparingOrPlaying() || playlist == null || currentTrackIndex < 0 || currentTrackIndex >= playlist.Count)
            {
                logger.Information(AppConstants.Logging.TrackPlaybackHandlerDiagnosticsLog.PlaybackStoppedDuringPrepareAborting);
                return false;
            }
        }
        finally
        {
            // Clear the flag after preparation is complete (whether successful or not)
            setIsPreparingTrack(false);
        }

        // Check again after WaitForMediaReadyAsync in case stop was called during the wait.
        // Only check playlist validity (null / index out of range) — a genuine StopAsync sets playlist to null.
        // Do NOT check isPreparingOrPlaying() here: IsPreparingTrack was just cleared in finally,
        // and AudioPlayer.Status may be Stopped from a MediaElement reset (e.g. CDN refresh replay)
        // even though no stop was requested.
        var playlistAfterWait = getPlaylist();
        if (playlistAfterWait == null || currentTrackIndex < 0 || currentTrackIndex >= playlistAfterWait.Count)
        {
            logger.Information(AppConstants.Logging.TrackPlaybackHandlerDiagnosticsLog.PlaybackStoppedDuringWaitAborting);
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

        // Seek ONLY on first encounter of a Bible track with saved progress
        // Conditions:
        // 1. Caller did not request start from beginning (manual Prev always, manual Next to already-visited)
        // 2. This is the FIRST encounter of this Bible track in this session (manual or automatic)
        // 3. It's a Bible track with saved progress
        // 4. Schedule allows resume (AlwaysPlayFromStart == false) - checked via ShouldResumeFromLastPositionAsync
        // Note: Auto-advance uses startFromBeginning=false; manual Prev uses true; manual Next uses true when track was already visited
        var inMemoryFinishedDuration = track.PlayItem?.Metadata?.FinishedDuration ?? TimeSpan.Zero;
        var shouldCheckSeek = !startFromBeginning
            && isFirstEncounter
            && isBibleTrack
            && inMemoryFinishedDuration != TimeSpan.Zero;

        if (shouldCheckSeek)
        {
            var shouldResume = await trackPreparationHandler.ShouldResumeFromLastPositionAsync(currentScheduleId);

            if (shouldResume)
            {
                seekPosition = inMemoryFinishedDuration;
                logger.Information(AppConstants.Logging.TrackPlaybackHandlerDiagnosticsLog.ResumeSeekFromInMemoryFinishedDuration,
                    seekPosition.Value, currentScheduleId);
            }
        }
        else if (!startFromBeginning && isFirstEncounter && isBibleTrack
            && inMemoryFinishedDuration == TimeSpan.Zero && currentScheduleId.HasValue
            && playedBibleTrackKeys.Count == 0)
        {
            // Fallback: in-memory FinishedDuration is zero but the DB may have a saved position
            // (e.g. after process restart if playlist builder didn't propagate the value).
            // Only applies to the very first Bible track in the session — once any Bible track
            // has played, the DB's FinishedDuration reflects that track's position and is stale
            // for subsequent tracks.
            var dbFinishedDuration = await trackPreparationHandler.GetScheduleFinishedDurationAsync(currentScheduleId);
            if (dbFinishedDuration > TimeSpan.Zero)
            {
                var shouldResume = await trackPreparationHandler.ShouldResumeFromLastPositionAsync(currentScheduleId);
                if (shouldResume)
                {
                    seekPosition = dbFinishedDuration;
                    logger.Warning(AppConstants.Logging.TrackPlaybackHandlerDiagnosticsLog.ResumeFallbackDbFinishedDuration,
                        dbFinishedDuration, currentScheduleId);
                }
            }
            else
            {
                logger.Debug(AppConstants.Logging.TrackPlaybackHandlerDiagnosticsLog.ResumeNoSeekBothZero,
                    currentScheduleId);
            }
        }

        // Mark this Bible track as played (after checking seek conditions, before actually playing)
        if (isBibleTrack && bibleTrackKey != null)
        {
            playedBibleTrackKeys.Add(bibleTrackKey);
        }

        // Validate seek position against track duration to prevent seeking past the end.
        // Seeking past the end causes ExoPlayer to immediately fire endedState, which triggers
        // auto-advance to the next track. This happens when FinishedDuration is stale
        // (e.g. from a previously played longer track after a publication/track change).
        // Only reject the seek when duration is positively known and the seek exceeds it.
        // When duration is still zero (not yet determined from stream), allow the seek through;
        // the post-play validation on iOS/Android will catch it once duration is accurate.
#if IOS || ANDROID
        var postPlayNeedsDurationRecheck = false;
#endif
        if (seekPosition.HasValue)
        {
            var prePlayDuration = audioPlayer.Duration;
            if (prePlayDuration > TimeSpan.Zero && seekPosition.Value >= prePlayDuration)
            {
                logger.Warning(AppConstants.Logging.TrackPlaybackHandlerDiagnosticsLog.ResumeSeekExceedsDurationStartingBeginning,
                    seekPosition.Value, prePlayDuration);
                seekPosition = null;
            }
#if IOS || ANDROID
            else if (prePlayDuration == TimeSpan.Zero)
            {
                postPlayNeedsDurationRecheck = true;
            }
#endif
        }

#if !IOS && !ANDROID
        // On non-iOS/Android platforms (Windows), seek BEFORE play
        if (seekPosition.HasValue)
        {
            await SeekWithRetryAsync(seekPosition.Value, cancellationToken);
        }
#endif

        cancellationToken.ThrowIfCancellationRequested();

        // Final check before starting playback - ensure stop wasn't called during seek/resume operations.
        // Same rationale as the post-WaitForMediaReadyAsync check: only check playlist validity.
        var playlistBeforePlay = getPlaylist();
        if (playlistBeforePlay == null || currentTrackIndex < 0 || currentTrackIndex >= playlistBeforePlay.Count)
        {
            logger.Information(AppConstants.Logging.TrackPlaybackHandlerDiagnosticsLog.PlaybackStoppedBeforePlayAborting);
            return false;
        }

        // Note: isPreparingTrack is already false at this point, so IsPreparingOrPlayingInternal
        // will rely on actual MediaElement state, which should be ready by now

        // When we need to seek to saved progress, mute before play so the user doesn't hear
        // audio from the beginning before the seek completes. Unmute after seek.
        var didMute = seekPosition.HasValue;
        if (didMute)
        {
            await audioPlayer.SetMutedAsync(true);
        }

        try
        {
            await audioPlayer.PlayAsync();

            // On iOS and Android, wait for playback to start so seekable ranges are available.
            // When seeking to resume: we muted above so no audible audio during this wait or the seek.
            // Use a longer delay (300ms) when resuming: after a track transition (e.g. music to Bible),
            // ExoPlayer/AVPlayer need extra time before seeking works reliably. 100ms is often insufficient.
#if IOS || ANDROID
            await Task.Delay(seekPosition.HasValue ? 300 : 100);

            // On iOS and Android, seek AFTER play starts - seekable ranges are more reliable once playing
            // On Android, this is critical when transitioning from music to Bible track because
            // SetSourceWithDummyQueue's player.SeekTo(currentItemIndex, 0) may interfere with seeking before play
            if (seekPosition.HasValue && postPlayNeedsDurationRecheck)
            {
                // Re-validate only when pre-play duration was unknown (zero). Now that playback
                // started, duration may be available from stream headers. When pre-play duration
                // was already known (> 0), the pre-play validation was sufficient; re-validating
                // here risks using a stale duration from a previous track (e.g. a short music
                // track whose duration is still cached in mediaElement.Duration), which would
                // incorrectly reject a valid seek position for the current Bible track.
                var postPlayDuration = audioPlayer.Duration;
                if (postPlayDuration > TimeSpan.Zero && seekPosition.Value >= postPlayDuration)
                {
                    logger.Warning(AppConstants.Logging.TrackPlaybackHandlerDiagnosticsLog.ResumePostPlaySeekExceedsDurationSkippingSeek,
                        seekPosition.Value, postPlayDuration);
                    seekPosition = null;
                }
            }

            if (seekPosition.HasValue)
            {
                await SeekWithRetryAsync(seekPosition.Value, cancellationToken);
                await Task.Delay(100, cancellationToken);
            }
#endif
        }
        finally
        {
            if (didMute)
            {
                await audioPlayer.SetMutedAsync(false);
            }
        }

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
    private async Task SeekWithRetryAsync(TimeSpan position, CancellationToken cancellationToken = default)
    {
#if IOS || ANDROID
        const int maxRetries = 10;
        const int retryDelayMs = 300;
        var seekBudgetStart = DateTime.UtcNow;
        var totalSeekBudget = TimeSpan.FromSeconds(15);

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (DateTime.UtcNow - seekBudgetStart > totalSeekBudget)
            {
                logger.Warning(AppConstants.Logging.TrackPlaybackHandlerDiagnosticsLog.SeekTotalBudgetExceeded,
                    totalSeekBudget.TotalSeconds, attempt - 1);
                return;
            }

            var outcome = await TrySeekSingleAttemptAsync(position, attempt, maxRetries, retryDelayMs, cancellationToken);
            if (outcome == SeekAttemptOutcome.Success || outcome == SeekAttemptOutcome.FatalError)
            {
                return;
            }
        }
#else
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            await audioPlayer.SeekToAsync(position);
        }
        catch (InvalidOperationException ex)
        {
            logger.Debug(ex, AppConstants.Logging.TrackPlaybackHandlerDiagnosticsLog.SeekFailedPlayerNotReadySeekableRanges);
        }
        catch (Exception ex)
        {
            // Catch any other exceptions during seek
            logger.Warning(ex, AppConstants.Logging.TrackPlaybackHandlerDiagnosticsLog.UnexpectedErrorDuringSeekResumeWillStartBeginning);
        }
#endif
    }

#if IOS || ANDROID
    private enum SeekAttemptOutcome
    {
        Retry,
        Success,
        FatalError
    }

    private async Task<SeekAttemptOutcome> TrySeekSingleAttemptAsync(
        TimeSpan position,
        int attempt,
        int maxRetries,
        int retryDelayMs,
        CancellationToken cancellationToken)
    {
        try
        {
            await audioPlayer.SeekToAsync(position);

            await Task.Delay(150, cancellationToken);
            var currentPos = audioPlayer.CurrentPosition;

            if (currentPos.HasValue && Math.Abs(currentPos.Value.TotalSeconds - position.TotalSeconds) < 1.0)
            {
                return SeekAttemptOutcome.Success;
            }

            logger.Warning(AppConstants.Logging.TrackPlaybackHandlerDiagnosticsLog.SeekAttemptWasNoOpWillRetry,
                attempt, position, currentPos);

            if (attempt < maxRetries)
            {
                await Task.Delay(retryDelayMs, cancellationToken);
            }
            else
            {
                logger.Error(AppConstants.Logging.TrackPlaybackHandlerDiagnosticsLog.SeekAllAttemptsNoOpStartingBeginning, maxRetries);
            }

            return SeekAttemptOutcome.Retry;
        }
        catch (InvalidOperationException ex)
        {
            logger.Warning(ex, AppConstants.Logging.TrackPlaybackHandlerDiagnosticsLog.SeekAttemptFailedInvalidOperation,
                attempt, ex.Message);
            if (attempt < maxRetries)
            {
                await Task.Delay(retryDelayMs, cancellationToken);
            }
            else
            {
                logger.Error(AppConstants.Logging.TrackPlaybackHandlerDiagnosticsLog.SeekAllAttemptsFailedStartingBeginning, maxRetries);
            }

            return SeekAttemptOutcome.Retry;
        }
        catch (Exception ex)
        {
            logger.Error(ex, AppConstants.Logging.TrackPlaybackHandlerDiagnosticsLog.SeekAttemptFailedUnexpectedError, attempt, ex.Message);
            return SeekAttemptOutcome.FatalError;
        }
    }
#endif
}

